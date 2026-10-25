// The CanNotDelete lock on one environment's database (design.md §10.1, S00.02.05). It is its own file because the
// deployment identity's Contributor role cannot write locks, so main.bicep, which the pipeline runs, must not hold one.
// The operator runs this file once per environment, as Owner, after that environment's first deploy, which creates the
// database; see infra/README.md.
//
// The lock stops the database being deleted, and with it the server, because Azure refuses to delete a server that
// holds a locked database; ops/first-use-in-test.md checks both by hand. Everything a deploy changes stays allowed:
// a CanNotDelete lock does not block writes.
targetScope = 'resourceGroup'

@description('The environment, test or prod.')
param environmentName 'test' | 'prod'

resource server 'Microsoft.Sql/servers@2025-01-01' existing = {
  name: 'sql-grow2notes-${environmentName}'
}

resource database 'Microsoft.Sql/servers/databases@2025-01-01' existing = {
  parent: server
  name: 'sqldb-grow2notes'
}

resource databaseLock 'Microsoft.Authorization/locks@2020-05-01' = {
  name: 'grow2notes-database'
  scope: database
  properties: {
    level: 'CanNotDelete'
    notes: 'Grow2Notes keeps its notes here. Remove this lock, as Owner, only to delete the database on purpose.'
  }
}
