// The test environment: what differs from prod is in design.md §10.3.
using 'main.bicep'

param environmentName = 'test'

// Liveness only, with no database call, so the free database can pause (design.md §10.8).
param healthCheckPath = '/healthz'

// The web app's default address, which App Service's own certificate covers (D62).
param appOrigin = 'https://app-grow2notes-test.azurewebsites.net'

// The Azure SQL free offer, which keeps test's database at no cost (design.md §10.2): a General Purpose serverless
// database of at most 4 vCores (2 here, the offer's default) and at most 32 GB, with local backup storage and at most
// 7 days of point-in-time restore. sql.bicep pauses it until the next month when the month's free amount is used up,
// and monitoring.bicep emails the developer when 10% of it is left.
param databaseSku = {
  name: 'GP_S_Gen5'
  tier: 'GeneralPurpose'
  family: 'Gen5'
  capacity: 2
}
param databaseMaxSizeBytes = 34359738368
param useFreeOffer = true
param backupStorageRedundancy = 'Local'
param pointInTimeRestoreDays = 7

// Placeholders only. Every deploy passes the real values as parameters, from the test environment's secrets, because
// the object ID belongs to the operator's tenant and the address may contain the company's name (D42, D60, D64, D65);
// sql.bicep and monitoring.bicep refuse them empty.
param sqlAdminGroupName = ''
param sqlAdminGroupObjectId = ''
param alertEmail = ''
