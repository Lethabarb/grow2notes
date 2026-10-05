// The web app's VNet integration subnet, which the SQL server and the vault accept traffic from instead of the internet
// (design.md §7.7, §10.1).
targetScope = 'resourceGroup'

@description('The environment, test or prod.')
param environmentName string

@description('The region. Defaults to the resource group\'s, Australia Southeast (D38).')
param location string = resourceGroup().location

@description('The tags for the VNet. Defaults to the resource group\'s.')
param tags object = resourceGroup().tags

// The VNet is not peered or connected to anything, so the range only has to hold the subnet, with room for another.
@description('The VNet\'s address space.')
param addressPrefix string = '10.0.0.0/24'

// App Service takes one address per plan instance, and twice that while it scales or upgrades the plan.
@description('The integration subnet\'s range, a /27 (design.md §10.1).')
param subnetPrefix string = '10.0.0.0/27'

var subnetName = 'snet-app'

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2025-07-01' = {
  name: 'vnet-grow2notes-${environmentName}'
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [
        addressPrefix
      ]
    }
    // Inline rather than a child resource, so redeploying the VNet never removes the subnet the web app is joined to.
    subnets: [
      {
        name: subnetName
        properties: {
          addressPrefix: subnetPrefix
          delegations: [
            {
              name: 'Microsoft.Web.serverFarms'
              properties: {
                serviceName: 'Microsoft.Web/serverFarms'
              }
            }
          ]
          // These let the SQL server's VNet rule and the vault's network rule name this subnet. Traffic to both still
          // goes to their public endpoints, so no private endpoint or private DNS zone is needed.
          serviceEndpoints: [
            {
              service: 'Microsoft.Sql'
            }
            {
              service: 'Microsoft.KeyVault'
            }
          ]
        }
      }
    ]
  }
}

resource subnet 'Microsoft.Network/virtualNetworks/subnets@2025-07-01' existing = {
  parent: virtualNetwork
  name: subnetName
}

@description('The integration subnet: the web app joins it, and the SQL and Key Vault network rules allow it.')
output subnetId string = subnet.id
