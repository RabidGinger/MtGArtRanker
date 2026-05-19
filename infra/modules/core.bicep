@description('Short environment name, e.g. dev, prod')
param environmentName string

@description('Azure region for all resources')
param location string

@description('Base name prefix for resources (3-12 chars, lowercase).')
param resourcePrefix string

param sqlAdminLogin string

@secure()
param sqlAdminPassword string

param sqlEntraAdminObjectId string
param sqlEntraAdminLogin string
param deployerPrincipalId string

var uniq = uniqueString(resourceGroup().id)
var baseName = '${resourcePrefix}-${environmentName}'
var sqlServerName = toLower('sql-${resourcePrefix}-${environmentName}-${take(uniq, 5)}')
var sqlDbName = 'mtgartranker'
var keyVaultName = toLower('kv-${take(resourcePrefix, 8)}-${environmentName}-${take(uniq, 4)}')
var appPlanName = 'plan-${baseName}'
var appServiceName = toLower('app-${baseName}-${take(uniq, 5)}')
var swaName = 'swa-${baseName}'
var lawName = 'log-${baseName}'
var aiName = 'appi-${baseName}'
var isProd = environmentName == 'prod'

// ---------------- Monitoring ----------------

resource law 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: lawName
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: aiName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: law.id
  }
}

// ---------------- Azure SQL ----------------

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    version: '12.0'
    publicNetworkAccess: 'Enabled'
    minimalTlsVersion: '1.2'
  }
}

resource sqlEntraAdmin 'Microsoft.Sql/servers/administrators@2023-08-01-preview' = {
  parent: sqlServer
  name: 'ActiveDirectory'
  properties: {
    administratorType: 'ActiveDirectory'
    login: sqlEntraAdminLogin
    sid: sqlEntraAdminObjectId
    tenantId: subscription().tenantId
  }
}

resource sqlAllowAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAllAzureIPs'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource sqlDb 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: sqlDbName
  location: location
  sku: {
    name: isProd ? 'S1' : 'Basic'
    tier: isProd ? 'Standard' : 'Basic'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    zoneRedundant: false
  }
}

// ---------------- Key Vault ----------------

resource kv 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: { family: 'A', name: 'standard' }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    publicNetworkAccess: 'Enabled'
  }
}

resource sqlConnSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: kv
  name: 'ConnectionStrings--Sql'
  properties: {
    value: 'Server=tcp:${sqlServerName}.database.windows.net,1433;Initial Catalog=${sqlDbName};Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
  }
}

// ---------------- App Service ----------------

resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: appPlanName
  location: location
  kind: 'linux'
  sku: {
    name: isProd ? 'S1' : 'B1'
    tier: isProd ? 'Standard' : 'Basic'
  }
  properties: { reserved: true }
}

resource app 'Microsoft.Web/sites@2024-04-01' = {
  name: appServiceName
  location: location
  kind: 'app,linux'
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: isProd
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      appSettings: [
        { name: 'ASPNETCORE_ENVIRONMENT', value: isProd ? 'Production' : 'Development' }
        { name: 'KeyVault__Uri', value: kv.properties.vaultUri }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
        { name: 'ApplicationInsightsAgent_EXTENSION_VERSION', value: '~3' }
        { name: 'XDT_MicrosoftApplicationInsights_Mode', value: 'Recommended' }
      ]
    }
  }
}

// Grant App MI access to read Key Vault secrets
var kvSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'
resource appKvRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(kv.id, app.id, kvSecretsUserRoleId)
  scope: kv
  properties: {
    principalId: app.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', kvSecretsUserRoleId)
  }
}

// ---------------- Static Web App ----------------

resource swa 'Microsoft.Web/staticSites@2024-04-01' = {
  name: swaName
  location: location
  sku: { name: 'Standard', tier: 'Standard' }
  properties: {
    provider: 'None'
  }
}

// Link App Service as backend for /api/* (Bring Your Own API)
resource swaBackend 'Microsoft.Web/staticSites/linkedBackends@2024-04-01' = {
  parent: swa
  name: 'api'
  properties: {
    backendResourceId: app.id
    region: location
  }
}

// ---------------- Optional deployer role ----------------

var contributorRoleId = 'b24988ac-6180-42a0-ab88-20f7382dd24c'
resource deployerRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(deployerPrincipalId)) {
  name: guid(resourceGroup().id, deployerPrincipalId, contributorRoleId)
  properties: {
    principalId: deployerPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', contributorRoleId)
  }
}

output apiHostname string = app.properties.defaultHostName
output swaHostname string = swa.properties.defaultHostname
output keyVaultName string = kv.name
output sqlServerName string = sqlServer.name
output sqlDatabaseName string = sqlDb.name
output appServiceName string = app.name
output swaName string = swa.name