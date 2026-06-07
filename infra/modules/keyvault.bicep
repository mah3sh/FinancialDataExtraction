param prefix string
param uniqueSuffix string
param location string
param tags object
param managedIdentityPrincipalId string

@secure()
param sqlConnectionString string
@secure()
param jwtKey string
@secure()
param openAiApiKey string
param openAiEndpoint string
param storageBlobEndpoint string
param storageContainerName string

// KV name: 3-24 chars
var kvName = take('${prefix}-kv-${uniqueSuffix}', 24)

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: kvName
  location: location
  tags: tags
  properties: {
    sku: { family: 'A', name: 'standard' }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true   // RBAC instead of legacy access policies
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    publicNetworkAccess: 'Enabled'
  }
}

// Key Vault Secrets User — read-only on secrets (principle of least privilege)
var kvSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

resource kvRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, managedIdentityPrincipalId, kvSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', kvSecretsUserRoleId)
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource secretSqlConn 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'SqlConnectionString'
  properties: { value: sqlConnectionString }
}

resource secretJwtKey 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'JwtKey'
  properties: { value: jwtKey }
}

resource secretOpenAiKey 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'AzureOpenAIApiKey'
  properties: { value: openAiApiKey }
}

resource secretOpenAiEndpoint 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'AzureOpenAIEndpoint'
  properties: { value: openAiEndpoint }
}

resource secretBlobEndpoint 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'StorageBlobEndpoint'
  properties: { value: storageBlobEndpoint }
}

resource secretContainerName 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'StorageContainerName'
  properties: { value: storageContainerName }
}

output uri string = keyVault.properties.vaultUri
output name string = keyVault.name
