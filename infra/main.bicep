// Azure resources for one environment (§23.7, §23.9): App Service (Linux) hosting the API, SPA and jobs;
// PostgreSQL Flexible Server with Entra authentication only; Key Vault; Application Insights.
// Deploy per environment: az deployment group create -g <rg> -f infra/main.bicep -p infra/env/<env>.bicepparam
targetScope = 'resourceGroup'

@allowed(['dev', 'test', 'review', 'prod'])
param env string
param location string = resourceGroup().location
param appSku string = 'P1v3'
param dbSku string = 'Standard_D2ds_v5'
param dbStorageGb int = 128
@description('Point-in-time restore window in days (Q9 default 14).')
param backupRetentionDays int = 14
param entraTenantId string
param apiAudience string
param spaClientId string
param apiScope string
@description('Object ID and name of the Entra group that administers the database.')
param dbAdminGroupObjectId string
param dbAdminGroupName string
@description('Operators alerted on errors, job failures, delayed evaluation and late digests (§22). Empty: no alert rules are deployed.')
param operatorEmail string = ''
@description('Reviewer public CIDR ranges. Review denies all inbound traffic until an authorised range is supplied.')
param reviewAllowedCidrs array = []

var name = 'hub-${env}'

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${name}-logs'
  location: location
  properties: { retentionInDays: env == 'prod' ? 90 : 30, sku: { name: 'PerGB2018' } }
}

resource insights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${name}-insights'
  location: location
  kind: 'web'
  properties: { Application_Type: 'web', WorkspaceResourceId: logs.id }
}

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: '${name}-kv'
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: { family: 'A', name: 'standard' }
    enableRbacAuthorization: true
    enablePurgeProtection: true
  }
}

resource db 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: '${name}-pg'
  location: location
  sku: { name: dbSku, tier: 'GeneralPurpose' }
  properties: {
    version: '17'
    storage: { storageSizeGB: dbStorageGb, autoGrow: 'Enabled' }
    backup: { backupRetentionDays: backupRetentionDays, geoRedundantBackup: 'Disabled' }
    highAvailability: { mode: 'Disabled' }
    authConfig: { activeDirectoryAuth: 'Enabled', passwordAuth: 'Disabled', tenantId: entraTenantId }
  }
}

resource dbName 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = { parent: db, name: 'hub' }

// The schema uses citext (case-insensitive keys and emails) and pg_trgm (contains-search); Azure requires allow-listing them.
resource dbExtensions 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  parent: db
  name: 'azure.extensions'
  properties: { value: 'CITEXT,PG_TRGM', source: 'user-override' }
}

resource dbAdmin 'Microsoft.DBforPostgreSQL/flexibleServers/administrators@2024-08-01' = {
  parent: db
  name: dbAdminGroupObjectId
  properties: { principalType: 'Group', principalName: dbAdminGroupName, tenantId: entraTenantId }
}

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${name}-plan'
  location: location
  sku: { name: appSku }
  kind: 'linux'
  properties: { reserved: true }
}

resource app 'Microsoft.Web/sites@2023-12-01' = {
  name: '${name}-app'
  location: location
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      alwaysOn: true
      healthCheckPath: '/health'
      ipSecurityRestrictionsDefaultAction: env == 'review' ? 'Deny' : 'Allow'
      ipSecurityRestrictions: [for (cidr, i) in reviewAllowedCidrs: {
        ipAddress: cidr
        action: 'Allow'
        priority: 100 + i
        name: 'reviewer-${i}'
      }]
      appSettings: [
        { name: 'ASPNETCORE_ENVIRONMENT', value: env == 'prod' ? 'Production' : 'Staging' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: insights.properties.ConnectionString }
        { name: 'Auth__Mode', value: env == 'review' ? 'LocalPassword' : 'Entra' }
        // Review sign-in uses individual password verifiers stored as a Key Vault secret, never in Bicep or Git.
        // Provision review-users in this vault before starting the review app. An unresolved reference fails startup.
        { name: 'Auth__Local__UsersJson', value: env == 'review' ? '@Microsoft.KeyVault(VaultName=${vault.name};SecretName=review-users)' : '' }
        { name: 'Auth__Local__KeyDirectory', value: env == 'review' ? '/home/hub-review-keys' : '' }
        { name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE', value: env == 'review' ? 'true' : 'false' }
        { name: 'Auth__Entra__TenantId', value: entraTenantId }
        { name: 'Auth__Entra__Audience', value: apiAudience }
        { name: 'Auth__Entra__SpaClientId', value: spaClientId }
        { name: 'Auth__Entra__ApiScope', value: apiScope }
        // The review resource group and database persist across preview releases.
        // Production never receives this opt-in setting or the review database.
        { name: 'Seed__ReviewDemo', value: env == 'review' ? 'true' : 'false' }
        // The app's managed identity signs in to PostgreSQL; no database password exists (§21).
        { name: 'Db__UseManagedIdentity', value: 'true' }
        { name: 'ConnectionStrings__Hub', value: 'Host=${db.properties.fullyQualifiedDomainName};Database=hub;Username=${name}-app;Ssl Mode=Require' }
        { name: 'Csp__ConnectSrc', value: 'https://*.applicationinsights.azure.com' }
      ]
    }
  }
}

// The app reads secrets (for example an SMTP relay password, if that mail route is chosen) from Key Vault.
resource vaultReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, app.id, 'secrets-user')
  scope: vault
  properties: {
    principalId: app.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
  }
}

output appHostName string = app.properties.defaultHostName
output appPrincipalId string = app.identity.principalId

// ---------- Operator alerts (§22, §23.8; packet 011 FR-006) ----------
// Deployed only when an operator address is given. The app logs "OpsAlert <kind>" errors (evaluation delayed, nightly
// run missed, job failed, digest late) and an "OpsHeartbeat" every five minutes; requests carry their result codes.
var alerting = !empty(operatorEmail)

resource operators 'Microsoft.Insights/actionGroups@2023-01-01' = if (alerting) {
  name: '${name}-operators'
  location: 'global'
  properties: {
    groupShortName: 'hubops'
    enabled: true
    emailReceivers: [ { name: 'operators', emailAddress: operatorEmail, useCommonAlertSchema: true } ]
  }
}

var alerts = [
  { key: 'ops', title: 'Hub operations alert', severity: 1, window: 'PT10M', query: 'traces | where message startswith "OpsAlert"', op: 'GreaterThan', threshold: 0 }
  { key: 'heartbeat', title: 'Hub background jobs stopped', severity: 1, window: 'PT30M', query: 'traces | where message == "OpsHeartbeat"', op: 'LessThan', threshold: 1 }
  { key: 'errors', title: 'Hub failed-request rate above 5 %', severity: 2, window: 'PT15M'
    query: 'requests | summarize total = count(), failed = countif(toint(resultCode) >= 500) | where total >= 20 and todouble(failed) / total > 0.05', op: 'GreaterThan', threshold: 0 }
]

resource alertRules 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = [for a in alerts: if (alerting) {
  name: '${name}-alert-${a.key}'
  location: location
  properties: {
    displayName: a.title
    severity: a.severity
    enabled: true
    scopes: [ insights.id ]
    evaluationFrequency: 'PT5M'
    windowSize: a.window
    criteria: { allOf: [ { query: a.query, timeAggregation: 'Count', operator: a.op, threshold: a.threshold, failingPeriods: { numberOfEvaluationPeriods: 1, minFailingPeriodsToAlert: 1 } } ] }
    actions: { actionGroups: [ operators.id ] }
  }
}]
