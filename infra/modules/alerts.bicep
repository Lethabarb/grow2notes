// The production operations alerts on the web app and the database (design.md §10.1, S06.04.01): server errors, a
// failing health check and a busy database. Each one emails the developer through monitoring.bicep's action group, and
// each closes itself once its condition clears. They hold resource metrics only, so they need no workspace queries.
// main.bicep deploys this file for prod alone. It is not part of monitoring.bicep, because these alerts watch the web
// app, and monitoring.bicep is an input to the web app's settings, so that would be a cycle. 2018-03-01 is the newest
// stable API version Bicep has types for.
targetScope = 'resourceGroup'

@description('The environment, test or prod.')
param environmentName string

@description('The tags for every resource here. Defaults to the resource group\'s.')
param tags object = resourceGroup().tags

@description('The action group that emails the developer, from monitoring.bicep.')
param actionGroupId string

@description('The web app, from app.bicep.')
param webAppId string

@description('The app\'s database, from sql.bicep.')
param databaseId string

// Any 5xx in five minutes. It can fire once around a deploy, while the new build starts, which is expected.
resource http5xxAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-grow2notes-${environmentName}-http-5xx'
  location: 'global'
  tags: tags
  properties: {
    description: 'The web app answered a request with a server error (HTTP 5xx).'
    severity: 2
    enabled: true
    scopes: [
      webAppId
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          criterionType: 'StaticThresholdCriterion'
          name: 'http-5xx'
          metricNamespace: 'Microsoft.Web/sites'
          metricName: 'Http5xx'
          timeAggregation: 'Total'
          operator: 'GreaterThan'
          threshold: 0
        }
      ]
    }
    autoMitigate: true
    actions: [
      {
        actionGroupId: actionGroupId
      }
    ]
  }
}

// App Service probes /healthz/ready, which includes the database, and reports 100 for a pass and 0 for a fail. Its
// finest grain is five minutes, and an average below 50 over 15 minutes means it failed for most of that time, so a
// restart during a deploy or a short database blip does not fire it. A stopped site reports nothing at all, so it
// raises no alert (the missing-data blind spot, recorded in design/backlog/tasks.md T22).
resource healthCheckAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-grow2notes-${environmentName}-health-check'
  location: 'global'
  tags: tags
  properties: {
    description: 'The web app\'s health check has been failing for most of the last 15 minutes.'
    severity: 1
    enabled: true
    scopes: [
      webAppId
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          criterionType: 'StaticThresholdCriterion'
          name: 'health-check-status'
          metricNamespace: 'Microsoft.Web/sites'
          metricName: 'HealthCheckStatus'
          timeAggregation: 'Average'
          operator: 'LessThan'
          threshold: 50
        }
      ]
    }
    autoMitigate: true
    actions: [
      {
        actionGroupId: actionGroupId
      }
    ]
  }
}

// The S0 database has a fixed 10 DTUs, so a sustained load near the limit slows every request.
resource databaseLoadAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-grow2notes-${environmentName}-dtu'
  location: 'global'
  tags: tags
  properties: {
    description: 'The database has used more than 80% of its DTUs on average over the last 15 minutes.'
    severity: 2
    enabled: true
    scopes: [
      databaseId
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          criterionType: 'StaticThresholdCriterion'
          name: 'dtu-consumption'
          metricNamespace: 'Microsoft.Sql/servers/databases'
          metricName: 'dtu_consumption_percent'
          timeAggregation: 'Average'
          operator: 'GreaterThan'
          threshold: 80
        }
      ]
    }
    autoMitigate: true
    actions: [
      {
        actionGroupId: actionGroupId
      }
    ]
  }
}
