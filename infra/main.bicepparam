using './main.bicep'

// ── Non-secret parameters (safe to commit) ────────────────────────────────────
param appName = 'docpipeline'
param environment = 'prod'
param location = 'eastus2'
param swaLocation = 'eastus2'
param sqlAdminLogin = 'sqladmin'
param sqlLocation = 'westus2'
param openAiLocation = 'swedencentral'
param openAiDeploymentName = 'gpt-4o'
param openAiTpmCapacity = 10

// ── Secrets — read from environment variables at deploy time (never commit values here)
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD', 'PLACEHOLDER-set-SQL_ADMIN_PASSWORD!')
param jwtKey = readEnvironmentVariable('JWT_KEY', 'PLACEHOLDER-set-JWT_KEY-env-var-before-deploying!!')
