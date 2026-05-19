targetScope = 'subscription'

@description('Short environment name, e.g. dev, prod')
@allowed([ 'dev', 'prod' ])
param environmentName string

@description('Azure region for all resources')
param location string = 'westus2'

@description('Base name prefix for resources (3-12 chars, lowercase).')
param resourcePrefix string = 'mtgartrank'

@description('Azure SQL administrator login')
param sqlAdminLogin string = 'sqladmin'

@secure()
@description('Azure SQL administrator password (stored only in Key Vault).')
param sqlAdminPassword string

@description('Azure AD object id (oid) of the SQL Entra admin (you).')
param sqlEntraAdminObjectId string

@description('Display name (UPN) of the SQL Entra admin (you).')
param sqlEntraAdminLogin string

@description('Object id of a principal (e.g. GitHub Actions service principal) that should be granted contributor on the RG. Leave empty to skip.')
param deployerPrincipalId string = ''

var rgName = 'rg-${resourcePrefix}-${environmentName}'

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: rgName
  location: location
}

module core 'modules/core.bicep' = {
  scope: rg
  name: 'core-${environmentName}'
  params: {
    environmentName: environmentName
    location: location
    resourcePrefix: resourcePrefix
    sqlAdminLogin: sqlAdminLogin
    sqlAdminPassword: sqlAdminPassword
    sqlEntraAdminObjectId: sqlEntraAdminObjectId
    sqlEntraAdminLogin: sqlEntraAdminLogin
    deployerPrincipalId: deployerPrincipalId
  }
}

output resourceGroupName string = rg.name
output apiHostname string = core.outputs.apiHostname
output swaHostname string = core.outputs.swaHostname
output keyVaultName string = core.outputs.keyVaultName
output sqlServerName string = core.outputs.sqlServerName
output sqlDatabaseName string = core.outputs.sqlDatabaseName