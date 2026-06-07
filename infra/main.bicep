targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────────────────────

@description('Base name for all resources. 3-16 lowercase alphanumeric.')
@minLength(3)
@maxLength(16)
param appName string = 'docpipeline'

@description('Deployment environment tag.')
@allowed(['dev', 'staging', 'prod'])
param environment string = 'prod'

@description('''
Azure region for all resources.
Azure OpenAI GPT-4o supported in: eastus, eastus2, australiaeast, canadaeast,
francecentral, japaneast, swedencentral, uksouth, westeurope, northcentralus.
''')
param location string = 'eastus2'

@description('Azure region for SQL Server. East US/East US 2 periodically close new-server provisioning; West US 2 is the recommended fallback.')
param sqlLocation string = 'westus2'

@description('Azure region for Azure OpenAI. Sweden Central consistently has available gpt-4o Standard quota for new subscriptions.')
param openAiLocation string = 'eastus2'

@description('SQL Server administrator login name.')
param sqlAdminLogin string = 'sqladmin'

@description('SQL administrator password. Min 12 chars, must include upper/lower/digit/special.')
@minLength(12)
@secure()
param sqlAdminPassword string

@description('JWT signing key. Minimum 32 characters. Generate: openssl rand -base64 48')
@minLength(32)
@secure()
param jwtKey string

@description('Azure OpenAI GPT-4o model deployment name.')
param openAiDeploymentName string = 'gpt-4o'

@description('GPT-4o capacity in thousands of tokens per minute.')
param openAiTpmCapacity int = 10


@description('Static Web App Azure region (limited availability).')
@allowed(['eastus2', 'centralus', 'westus2', 'westeurope', 'eastasia'])
param swaLocation string = 'eastus2'

// ── Naming ────────────────────────────────────────────────────────────────────

var prefix = toLower('${appName}-${environment}')
var uniqueSuffix = take(uniqueString(resourceGroup().id), 6)

var tags = {
  Application: appName
  Environment: environment
  ManagedBy: 'Bicep'
}

// ── Modules ───────────────────────────────────────────────────────────────────

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: { prefix: prefix, location: location, tags: tags }
}

module identity 'modules/identity.bicep' = {
  name: 'identity'
  params: { prefix: prefix, location: location, tags: tags }
}

module acr 'modules/acr.bicep' = {
  name: 'acr'
  params: {
    prefix: prefix
    uniqueSuffix: uniqueSuffix
    location: location
    tags: tags
    managedIdentityPrincipalId: identity.outputs.principalId
  }
}

module sql 'modules/sql.bicep' = {
  name: 'sql'
  params: {
    prefix: prefix
    uniqueSuffix: uniqueSuffix
    location: sqlLocation
    tags: tags
    adminLogin: sqlAdminLogin
    adminPassword: sqlAdminPassword
  }
}

module openai 'modules/openai.bicep' = {
  name: 'openai'
  params: {
    prefix: prefix
    uniqueSuffix: uniqueSuffix
    location: openAiLocation
    tags: tags
    deploymentName: openAiDeploymentName
    tpmCapacity: openAiTpmCapacity
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    prefix: prefix
    uniqueSuffix: uniqueSuffix
    location: location
    tags: tags
    managedIdentityPrincipalId: identity.outputs.principalId
  }
}

module keyvault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  params: {
    prefix: prefix
    uniqueSuffix: uniqueSuffix
    location: location
    tags: tags
    managedIdentityPrincipalId: identity.outputs.principalId
    // Build connection string from SQL module outputs — never hardcode in param file
    sqlConnectionString: 'Server=${sql.outputs.serverFqdn},1433;Database=DocPipeline;User Id=${sqlAdminLogin};Password=${sqlAdminPassword};TrustServerCertificate=False;Encrypt=True;Connection Timeout=30;'
    jwtKey: jwtKey
    openAiApiKey: openai.outputs.apiKey
    openAiEndpoint: openai.outputs.endpoint
    storageBlobEndpoint: storage.outputs.blobEndpoint
    storageContainerName: storage.outputs.containerName
  }
}

// Static Web App first — its hostname is needed for Container App CORS
module staticwebapp 'modules/staticwebapp.bicep' = {
  name: 'staticwebapp'
  params: {
    prefix: prefix
    swaLocation: swaLocation
    tags: tags
  }
}

module containerapp 'modules/containerapp.bicep' = {
  name: 'containerapp'
  params: {
    prefix: prefix
    location: location
    tags: tags
    logAnalyticsCustomerId: monitoring.outputs.logAnalyticsCustomerId
    logAnalyticsSharedKey: monitoring.outputs.logAnalyticsSharedKey
    managedIdentityId: identity.outputs.id
    managedIdentityClientId: identity.outputs.clientId
    acrLoginServer: acr.outputs.loginServer
    keyVaultUri: keyvault.outputs.uri
    openAiDeploymentName: openAiDeploymentName
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    // CORS: allow the deployed SWA origin
    allowedCorsOrigins: [ 'https://${staticwebapp.outputs.defaultHostname}' ]
  }
}

// ── Outputs ───────────────────────────────────────────────────────────────────

output apiUrl string = 'https://${containerapp.outputs.fqdn}'
output frontendUrl string = 'https://${staticwebapp.outputs.defaultHostname}'
output acrLoginServer string = acr.outputs.loginServer
output containerAppName string = containerapp.outputs.name
output containerAppEnvName string = containerapp.outputs.envName
output staticWebAppName string = staticwebapp.outputs.name
output keyVaultName string = keyvault.outputs.name
output openAiEndpoint string = openai.outputs.endpoint
output resourceGroupName string = resourceGroup().name

@secure()
output swaDeploymentToken string = staticwebapp.outputs.deploymentToken
