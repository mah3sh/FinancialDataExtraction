param prefix string
param uniqueSuffix string
param location string
param tags object
param deploymentName string

@description('''
GPT-4o Tokens Per Minute capacity in thousands.
Minimum: 1 (= 1K TPM). Increase for production load.
Quota varies by region — check your subscription limits.
''')
param tpmCapacity int = 10

var accountName = '${prefix}-oai-${uniqueSuffix}'

resource openAiAccount 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: accountName
  location: location
  tags: tags
  kind: 'OpenAI'
  sku: { name: 'S0' }
  properties: {
    customSubDomainName: accountName
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: false       // key-based auth; swap to managed identity for zero-secret
    restrictOutboundNetworkAccess: false
  }
}

resource gpt4oDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: openAiAccount
  name: deploymentName
  sku: {
    name: 'Standard'
    capacity: tpmCapacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4o'
      version: '2024-11-20'
    }
    versionUpgradeOption: 'OnceCurrentVersionExpired'
  }
}

output endpoint string = openAiAccount.properties.endpoint
output accountName string = openAiAccount.name

@secure()
output apiKey string = openAiAccount.listKeys('2024-10-01').key1
