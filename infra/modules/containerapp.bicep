param prefix string
param location string
param tags object
param logAnalyticsCustomerId string

@secure()
param logAnalyticsSharedKey string

param managedIdentityId string
param managedIdentityClientId string
param acrLoginServer string
param keyVaultUri string
param openAiDeploymentName string
param appInsightsConnectionString string
param allowedCorsOrigins array

var envName = '${prefix}-cae'
var appName = '${prefix}-api'
// KV URI always ends with '/', so format is: {kvUri}secrets/{name}
var kvSecretUrl = '${keyVaultUri}secrets'

resource caEnvironment 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: envName
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsCustomerId
        sharedKey: logAnalyticsSharedKey
      }
    }
  }
}

resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: appName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${managedIdentityId}': {} }
  }
  properties: {
    managedEnvironmentId: caEnvironment.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        // CORS handled by ASP.NET Core middleware — keeps it DRY
      }
      // Pull from ACR using managed identity (no admin password needed)
      registries: [
        {
          server: acrLoginServer
          identity: managedIdentityId
        }
      ]
      // Secrets pulled from Key Vault at runtime via managed identity
      secrets: [
        {
          name: 'sql-connection-string'
          keyVaultUrl: '${kvSecretUrl}/SqlConnectionString'
          identity: managedIdentityId
        }
        {
          name: 'jwt-key'
          keyVaultUrl: '${kvSecretUrl}/JwtKey'
          identity: managedIdentityId
        }
        {
          name: 'openai-api-key'
          keyVaultUrl: '${kvSecretUrl}/AzureOpenAIApiKey'
          identity: managedIdentityId
        }
        {
          name: 'openai-endpoint'
          keyVaultUrl: '${kvSecretUrl}/AzureOpenAIEndpoint'
          identity: managedIdentityId
        }
        {
          name: 'storage-blob-endpoint'
          keyVaultUrl: '${kvSecretUrl}/StorageBlobEndpoint'
          identity: managedIdentityId
        }
        {
          name: 'storage-container-name'
          keyVaultUrl: '${kvSecretUrl}/StorageContainerName'
          identity: managedIdentityId
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            { name: 'ASPNETCORE_URLS', value: 'http://+:8080' }
            { name: 'ConnectionStrings__DefaultConnection', secretRef: 'sql-connection-string' }
            { name: 'Jwt__Key', secretRef: 'jwt-key' }
            { name: 'Jwt__Issuer', value: 'docpipeline' }
            { name: 'Jwt__Audience', value: 'docpipeline' }
            { name: 'Jwt__ExpiresMinutes', value: '60' }
            { name: 'AzureOpenAI__ApiKey', secretRef: 'openai-api-key' }
            { name: 'AzureOpenAI__Endpoint', secretRef: 'openai-endpoint' }
            { name: 'AzureOpenAI__DeploymentName', value: openAiDeploymentName }
            { name: 'Storage__UseAzureBlob', value: 'true' }
            { name: 'Storage__BlobEndpoint', secretRef: 'storage-blob-endpoint' }
            { name: 'Storage__ContainerName', secretRef: 'storage-container-name' }
            { name: 'Storage__ManagedIdentityClientId', value: managedIdentityClientId }
            { name: 'UseMockAI', value: 'false' }
            // CORS: allow SWA + local dev
            { name: 'Cors__AllowedOrigins__0', value: allowedCorsOrigins[0] }
            { name: 'Cors__AllowedOrigins__1', value: 'http://localhost:5173' }
            { name: 'ApplicationInsights__ConnectionString', value: appInsightsConnectionString }
          ]
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          probes: [
            {
              type: 'Liveness'
              httpGet: { path: '/health', port: 8080 }
              initialDelaySeconds: 20
              periodSeconds: 30
              failureThreshold: 3
            }
            {
              type: 'Readiness'
              httpGet: { path: '/health', port: 8080 }
              initialDelaySeconds: 10
              periodSeconds: 10
              failureThreshold: 5
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 5
        rules: [
          {
            name: 'http-concurrency'
            http: { metadata: { concurrentRequests: '20' } }
          }
        ]
      }
    }
  }
}

output fqdn string = containerApp.properties.configuration.ingress.fqdn
output name string = containerApp.name
output envName string = caEnvironment.name
