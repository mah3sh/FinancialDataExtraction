param prefix string
param uniqueSuffix string
param location string
param tags object
param adminLogin string

@minLength(12)
@secure()
param adminPassword string

var serverName = '${prefix}-sql-${uniqueSuffix}'

resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {
  name: serverName
  location: location
  tags: tags
  properties: {
    administratorLogin: adminLogin
    administratorLoginPassword: adminPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// Required so Container Apps (and other Azure services) can reach SQL
resource firewallAzureServices 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// General Purpose Serverless — cost-effective, auto-pauses after 60 min idle
resource database 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {
  parent: sqlServer
  name: 'DocPipeline'
  location: location
  tags: tags
  sku: {
    name: 'GP_S_Gen5'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 1
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 34359738368   // 32 GB max
    autoPauseDelay: 60          // pause after 60 min inactivity
    minCapacity: any('0.5')  // Bicep types this as int but ARM accepts float; any() bypasses the false BCP036 warning
    requestedBackupStorageRedundancy: 'Local'
    zoneRedundant: false
  }
}

output serverFqdn string = sqlServer.properties.fullyQualifiedDomainName
output serverName string = sqlServer.name
output databaseName string = database.name
