param prefix string
param uniqueSuffix string
param location string
param tags object
param managedIdentityPrincipalId string

// ACR names must be alphanumeric, 5-50 chars
var acrName = take(toLower(replace('${prefix}acr${uniqueSuffix}', '-', '')), 50)

resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: acrName
  location: location
  tags: tags
  sku: { name: 'Basic' }
  properties: {
    adminUserEnabled: false       // managed identity pull — no admin creds needed
    anonymousPullEnabled: false
    publicNetworkAccess: 'Enabled'
  }
}

// AcrPull built-in role
var acrPullRoleId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'

resource acrPullAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, managedIdentityPrincipalId, acrPullRoleId)
  scope: acr
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output loginServer string = acr.properties.loginServer
output name string = acr.name
