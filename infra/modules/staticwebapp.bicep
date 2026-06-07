param prefix string
param tags object

// SWA supported locations: eastus2, centralus, westus2, westeurope, eastasia
param swaLocation string = 'eastus2'

resource staticWebApp 'Microsoft.Web/staticSites@2023-01-01' = {
  name: '${prefix}-swa'
  location: swaLocation
  tags: tags
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    stagingEnvironmentPolicy: 'Disabled'
    allowConfigFileUpdates: true
    enterpriseGradeCdnStatus: 'Disabled'
  }
}

output name string = staticWebApp.name
output defaultHostname string = staticWebApp.properties.defaultHostname

@secure()
output deploymentToken string = staticWebApp.listSecrets().properties.apiKey
