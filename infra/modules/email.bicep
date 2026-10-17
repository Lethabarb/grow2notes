// Communication Services and its Email service, which send the setup-link email, the only email the app sends
// (design.md §7.1, §10.1, A23). The sender is the Email service's free Azure-managed domain, a subdomain of
// azurecomm.net whose SPF, DKIM and DMARC Azure sets, as test needs (A58); the sender on the product domain comes with
// production (S00.02.04).
targetScope = 'resourceGroup'

@description('The environment, test or prod.')
param environmentName string

@description('The tags for every resource here. Defaults to the resource group\'s.')
param tags object = resourceGroup().tags

// Both resources are global, and keep their data at rest in the geography named here, in a data centre Microsoft picks,
// which may be outside Victoria (design.md §10). Microsoft Learn lists Australia among Communication Services' data
// locations, and says email is processed in the resource's data location (checked 14 October 2026). A domain can be
// linked only to a Communication Services resource in the same geography, so both take this one value.
var dataLocation = 'Australia'

// It has no access keys of its own: it holds the domain, and the email is sent through Communication Services.
resource emailService 'Microsoft.Communication/emailServices@2026-03-18' = {
  name: 'ecs-grow2notes-${environmentName}'
  location: 'global'
  tags: tags
  properties: {
    dataLocation: dataLocation
  }
}

// Azure requires this name for its managed domain, and creates the domain's one sender, DoNotReply, with it. The email
// is never tracked (A23), which the sender also asks of each message, so engagement tracking is written out as off.
resource azureManagedDomain 'Microsoft.Communication/emailServices/domains@2026-03-18' = {
  parent: emailService
  name: 'AzureManagedDomain'
  location: 'global'
  tags: tags
  properties: {
    domainManagement: 'AzureManaged'
    userEngagementTracking: 'Disabled'
  }
}

resource communicationServices 'Microsoft.Communication/communicationServices@2026-03-18' = {
  name: 'acs-grow2notes-${environmentName}'
  location: 'global'
  tags: tags
  properties: {
    dataLocation: dataLocation
    // Access keys off, so the service accepts only an Entra token from an identity with a role on it: the app
    // identity, which bootstrap.bicep gives Communication and Email Service Owner on the group (design.md §9.4). So
    // the app is given no connection string or key.
    disableLocalAuth: true
    linkedDomains: [
      azureManagedDomain.id
    ]
  }
}

@description('Communication Services\' endpoint, which the app reads as Email:Endpoint.')
output endpoint string = 'https://${communicationServices.properties.hostName}'

@description('The managed domain\'s DoNotReply address, which the app reads as Email:SenderAddress and sends from.')
output senderAddress string = 'DoNotReply@${azureManagedDomain.properties.fromSenderDomain}'
