// The SQL server's firewall rules for a web app with no VNet integration: test's, on the free F1 plan (D105). One rule
// per address the app's App Service stamp can send from. Other customers' apps on that stamp share those addresses, so
// the rules let them reach the server's endpoint too; Entra-only sign-in is what keeps them out (ops/security-review.md
// F-27). Deployed after the app, whose addresses are known only then; main.bicep leaves it out for a B1 app, which
// arrives through the subnet's VNet rule instead.
targetScope = 'resourceGroup'

@description('The environment, test or prod.')
param environmentName string

@description('The IPv4 addresses the web app\'s outbound traffic can come from, from app.bicep.')
param ipAddresses string[]

resource server 'Microsoft.Sql/servers@2025-01-01' existing = {
  name: 'sql-grow2notes-${environmentName}'
}

// Named by position in the sorted list, so a later deploy overwrites them in place. A rule left over after the app's
// address list shrinks names an address App Service no longer uses for this app, and is deleted by hand
// (infra/README.md, Test on the free plan).
resource appOutboundRules 'Microsoft.Sql/servers/firewallRules@2025-01-01' = [
  for (ip, i) in sort(ipAddresses, (a, b) => a < b): {
    parent: server
    name: 'app-outbound-${i}'
    properties: {
      startIpAddress: ip
      endIpAddress: ip
    }
  }
]
