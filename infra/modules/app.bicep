// The App Service plan and the web app (design.md §7.1, §10.1). main.bicep builds the app settings from the other
// modules' outputs and passes them in; this module writes them.
targetScope = 'resourceGroup'

@description('The environment, test or prod.')
param environmentName string

@description('The region. Defaults to the resource group\'s, Australia Southeast (D38).')
param location string = resourceGroup().location

@description('The tags for the plan and the web app. Defaults to the resource group\'s.')
param tags object = resourceGroup().tags

@description('The resource ID of the app identity, id-grow2notes-<env>-app, which bootstrap.bicep creates.')
param appIdentityId string

@description('The VNet integration subnet, from network.bicep.')
param subnetId string

@description('The path App Service probes: /healthz in test, /healthz/ready in prod (design.md §10.3, §10.8).')
param healthCheckPath string

// App Service on Linux needs '__' in place of ':' in app setting names, so ConnectionStrings:Grow2Notes is passed as
// ConnectionStrings__Grow2Notes, which ASP.NET Core reads back as the same key.
@description('The app settings, by name. Every deploy replaces the whole set.')
param appSettings { *: string }

resource plan 'Microsoft.Web/serverfarms@2025-03-01' = {
  name: 'asp-grow2notes-${environmentName}'
  location: location
  tags: tags
  kind: 'linux'
  sku: {
    name: 'B1'
    capacity: 1
  }
  properties: {
    // This is what makes the plan Linux; kind alone does not.
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2025-03-01' = {
  name: 'app-grow2notes-${environmentName}'
  location: location
  tags: tags
  kind: 'app,linux'
  // Only the app identity, which bootstrap.bicep gave its roles and grant-identities.sql makes a database user
  // (design.md §9.4). There is no system-assigned identity.
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${appIdentityId}': {}
    }
  }
  properties: {
    serverFarmId: plan.id
    // HTTP is redirected to HTTPS. The address is the default *.azurewebsites.net one, which App Service's own
    // certificate covers, so there is no custom domain or certificate here (D62).
    httpsOnly: true
    // design.md names only the session cookie, and one instance needs no affinity, so no ARRAffinity cookies.
    clientAffinityEnabled: false
    virtualNetworkSubnetId: subnetId
    // Only the traffic to SQL and Key Vault, through the subnet's service endpoints, goes into the VNet (design.md
    // §7.7); Entra ID, email and telemetry go out directly from App Service, so the subnet needs no NAT gateway.
    // Written out because the portal's VNet integration turns application traffic routing on, and a deploy resets it.
    outboundVnetRouting: {
      allTraffic: false
      applicationTraffic: false
    }
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: true
      http20Enabled: true
      // A minimum, so clients that support TLS 1.3 still get it (design.md §9.3).
      minTlsVersion: '1.2'
      scmMinTlsVersion: '1.2'
      ftpsState: 'Disabled'
      healthCheckPath: healthCheckPath
      appSettings: [
        for setting in items(appSettings): {
          name: setting.key
          value: setting.value
        }
      ]
    }
  }
}

// The pipeline deploys with its Entra ID sign-in (design.md §10.4), so neither FTP nor the Kudu (scm) site needs a
// username and password (design.md §9.4). Two resources rather than a loop, because Bicep can type-check each policy
// only when its name is a literal.
resource ftpPublishingCredentials 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2025-03-01' = {
  parent: webApp
  name: 'ftp'
  properties: {
    allow: false
  }
}

resource scmPublishingCredentials 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2025-03-01' = {
  parent: webApp
  name: 'scm'
  properties: {
    allow: false
  }
}

@description('The web app\'s name, which the deploy step targets.')
output webAppName string = webApp.name

@description('The web app\'s default host name, <name>.azurewebsites.net, where it is reached and smoke-tested.')
output defaultHostName string = webApp.properties.defaultHostName
