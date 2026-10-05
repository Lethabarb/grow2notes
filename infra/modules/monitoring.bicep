// The developer's operations alerts (design.md §10.1, §10.3, §10.5): the action group that emails the developer, and
// the free offer's "free amount remaining" alert on the test database.
targetScope = 'resourceGroup'

@description('The environment, test or prod.')
param environmentName string

@description('The tags for the action group and the alert rule. Defaults to the resource group\'s.')
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
