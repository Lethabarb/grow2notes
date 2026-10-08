// The app's telemetry and the developer's operations alerts (design.md §9.5, §10.1, §10.3, §10.5): the Log Analytics
// workspace and the workspace-based Application Insights that the app sends its server telemetry to, the action group
// that emails the developer, and the free offer's "free amount remaining" alert on the test database.
targetScope = 'resourceGroup'

@description('The environment, test or prod.')
param environmentName string

@description('The region. Defaults to the resource group\'s, Australia Southeast (D38).')
param location string = resourceGroup().location

@description('The tags for every resource here. Defaults to the resource group\'s.')
param tags object = resourceGroup().tags

// Passed in by every deploy and never committed, because it may contain the company's name (D42, D65). The parameters
// files hold an empty placeholder, so a deploy that forgets it fails on this length. It is checked here rather than in
// main.bicep, because there Bicep would refuse to build the parameters files' placeholder.
@description('The developer\'s email address, which the alerts are sent to.')
@minLength(3)
param alertEmail string

@description('The app\'s database, from sql.bicep.')
param databaseId string

@description('Whether the database uses the Azure SQL free offer, which is what the free amount alert watches.')
param useFreeOffer bool

// 30 days' retention and a 0.5 GB daily ingestion cap (A37), the same in test and prod (design.md §10.1, §10.3). At
// the cap the workspace drops new telemetry until its next daily reset, which bounds the cost.
var retentionInDays = 30

resource workspace 'Microsoft.OperationalInsights/workspaces@2025-07-01' = {
  name: 'log-grow2notes-${environmentName}'
  location: location
  tags: tags
  properties: {
    // Pay-as-you-go.
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: retentionInDays
    workspaceCapping: {
      // Bicep has no decimal literals and types this as an int, but the API takes a decimal number of GB.
      dailyQuotaGb: json('0.5')
    }
    features: {
      // The workspace's shared keys stop working, so only Entra ID sign-ins can send data to it (design.md §9.5).
      disableLocalAuth: true
      // Querying takes a role on the workspace itself or above it, not only on Application Insights (design.md §9.5).
      // This is the default, written out so that a deploy undoes any change to it.
      enableLogAccessUsingOnlyResourcePermissions: false
      // Without it, Microsoft says, a workspace with 30-day retention may keep data for a 31st day.
      immediatePurgeDataOn30Days: true
    }
  }
}

// Microsoft's list of the tables that workspace-based Application Insights writes to. They keep data for 90 days by
// default, even when the workspace keeps 30, so each is set to the same 30 days, with no long-term retention after.
resource applicationInsightsTables 'Microsoft.OperationalInsights/workspaces/tables@2025-07-01' = [
  for table in [
    'AppAvailabilityResults'
    'AppBrowserTimings'
    'AppDependencies'
    'AppEvents'
    'AppExceptions'
    'AppGenAIContent'
    'AppMetrics'
    'AppPageViews'
    'AppPerformanceCounters'
    'AppRequests'
    'AppSystemEvents'
    'AppTraces'
  ]: {
    parent: workspace
    name: table
    properties: {
      retentionInDays: retentionInDays
      totalRetentionInDays: retentionInDays
    }
  }
]

// Workspace-based, so the telemetry is stored in the workspace above, under its cap and the tables' retention. The app
// sends it from App Service straight to the public ingestion endpoint, not through the VNet (design.md §7.7), so public
// access stays on. With local auth off, that endpoint accepts only an Entra token from an identity with the Monitoring
// Metrics Publisher role, which bootstrap.bicep gives the app identity (design.md §9.4). 2020-02-02 is the newest
// stable API version Bicep has types for.
resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-grow2notes-${environmentName}'
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspace.id
    IngestionMode: 'LogAnalytics'
    DisableLocalAuth: true
    // Ingestion uses a client's IP address only to look up its location, and stores 0.0.0.0 in its place, so no
    // address is kept (design.md §9.5). Written out so that a deploy undoes it if masking is ever turned off.
    DisableIpMasking: false
  }
}

// These are operations alerts for the developer, not app emails (design.md §10.1). Azure makes action groups and alert
// rules global; the Allowed locations policy lets global resources through.
resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: 'ag-grow2notes-${environmentName}'
  location: 'Global'
  tags: tags
  properties: {
    groupShortName: 'g2n-${environmentName}'
    enabled: true
    emailReceivers: [
      {
        name: 'developer'
        emailAddress: alertEmail
        useCommonAlertSchema: true
      }
    ]
  }
}

// The free offer gives the database 100,000 vCore seconds a month, and sql.bicep pauses it until the next month when
// they are used up, which stops the environment until then. The rule warns at 10,000 left, 10% of the month's amount,
// the level Microsoft's free offer guidance suggests. 2018-03-01 is the newest stable API version Bicep has types for.
resource freeAmountAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = if (useFreeOffer) {
  name: 'alert-grow2notes-${environmentName}-free-amount'
  location: 'global'
  tags: tags
  properties: {
    description: 'Less than 10% of this month\'s free vCore seconds are left; the database pauses when they run out.'
    severity: 2
    enabled: true
    scopes: [
      databaseId
    ]
    // The metric's finest grain is 15 minutes, so the rule checks that often. The amount only falls during a month, so
    // the hour's lowest sample is the current amount.
    evaluationFrequency: 'PT15M'
    windowSize: 'PT1H'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          criterionType: 'StaticThresholdCriterion'
          name: 'free-amount-remaining'
          metricNamespace: 'Microsoft.Sql/servers/databases'
          metricName: 'free_amount_remaining'
          timeAggregation: 'Minimum'
          operator: 'LessThan'
          threshold: 10000
        }
      ]
    }
    // So the alert closes itself once the amount renews at the start of the month.
    autoMitigate: true
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
}

// A plain output rather than @secure(): the string is not a credential, because local auth is off, and app.bicep's
// parameters record it in the deployment history in any case, as they do the app identity's client ID. It still holds
// the component's IDs, so deploy.yml masks it in the public log (D60, D65).
@description('Application Insights\' connection string, which the app reads as APPLICATIONINSIGHTS_CONNECTION_STRING.')
output applicationInsightsConnectionString string = applicationInsights.properties.ConnectionString
