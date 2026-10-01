// Azure resources for one environment (§23.7, §23.9): App Service (Linux) hosting the API, SPA and jobs;
// PostgreSQL Flexible Server with Entra authentication only; Key Vault; Application Insights.
// Deploy per environment: az deployment group create -g <rg> -f infra/main.bicep -p infra/env/<env>.bicepparam
targetScope = 'resourceGroup'

@allowed(['dev', 'test', 'review', 'prod'])
param env string
@description('Optional globally unique suffix for resource names. Leave empty to use hub-<env>.')
param nameSuffix string = ''
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
@description('Optional App Service regional VNet integration subnet resource ID.')
param appSubnetResourceId string = ''
@description('Optional subnet resource ID for the PostgreSQL private endpoint.')
param dbPrivateEndpointSubnetResourceId string = ''
@description('Optional private DNS zone resource ID for the PostgreSQL private endpoint.')
param dbPrivateDnsZoneResourceId string = ''
@description('Optional verified hostname for App Service. Requires appCertificateThumbprint.')
param appHostname string = ''
@description('Certificate thumbprint for appHostname. The certificate must already exist in App Service.')
param appCertificateThumbprint string = ''
@description('Allowed host header list. Empty preserves the current wildcard until the company hostname is approved.')
param allowedHosts string = ''
@description('Public base URL used in notification and digest links.')
param emailBaseUrl string = ''
@allowed(['Log', 'Smtp'])
param emailMode string = 'Log'
param emailFrom string = ''
param emailSmtpHost string = ''
param emailSmtpPort string = '587'
param smtpPasswordSecretName string = ''
param graphDirectorySync bool = false
param graphMail bool = false
param graphMailbox string = ''
param graphManagedIdentityClientId string = ''
@description('Optional Entra group object ID for operator Key Vault Secrets Officer access.')
param operatorGroupObjectId string = ''
@description('Enable a disabled-by-default staging slot for controlled cutover.')
param enableDeploymentSlot bool = false
@description('Operators alerted on errors, job failures, delayed evaluation and late digests (§22). Empty: no alert rules are deployed.')
param operatorEmail string = ''
@description('Reviewer public CIDR ranges. Review denies all inbound traffic until an authorised range is supplied.')
param reviewAllowedCidrs array = []

var name = empty(nameSuffix) ? 'hub-${env}' : 'hub-${env}-${nameSuffix}'
// Do not switch to private-only database access until the app VNet path and both private-link inputs are supplied together.
var privateDatabase = !empty(appSubnetResourceId) && !empty(dbPrivateEndpointSubnetResourceId) && !empty(dbPrivateDnsZoneResourceId)
var hostAllowList = empty(allowedHosts) ? '*' : allowedHosts
var appSettings = [
  { name: 'ASPNETCORE_ENVIRONMENT', value: env == 'prod' ? 'Production' : 'Staging' }
  { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: insights.properties.ConnectionString }
  { name: 'AllowedHosts', value: hostAllowList }
  { name: 'Auth__Mode', value: env == 'review' ? 'LocalPassword' : 'Entra' }
  // Review sign-in uses individual password verifiers stored as a Key Vault secret, never in Bicep or Git.
  { name: 'Auth__Local__UsersJson', value: env == 'review' ? '@Microsoft.KeyVault(VaultName=${vault.name};SecretName=review-users)' : '' }
  { name: 'Auth__Local__KeyDirectory', value: env == 'review' ? '/home/hub-review-keys' : '' }
  { name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE', value: env == 'review' ? 'true' : 'false' }
  { name: 'Auth__Entra__TenantId', value: entraTenantId }
  { name: 'Auth__Entra__Audience', value: apiAudience }
  { name: 'Auth__Entra__SpaClientId', value: spaClientId }
  { name: 'Auth__Entra__ApiScope', value: apiScope }
  { name: 'Graph__DirectorySync', value: string(graphDirectorySync) }
  { name: 'Graph__Mail', value: string(graphMail) }
  { name: 'Graph__Mailbox', value: graphMailbox }
  { name: 'Graph__ManagedIdentityClientId', value: graphManagedIdentityClientId }
  { name: 'Email__Mode', value: emailMode }
  { name: 'Email__From', value: emailFrom }
  { name: 'Email__BaseUrl', value: emailBaseUrl }
  { name: 'Email__Smtp__Host', value: emailSmtpHost }
  { name: 'Email__Smtp__Port', value: emailSmtpPort }
  { name: 'Email__Smtp__Password', value: empty(smtpPasswordSecretName) ? '' : '@Microsoft.KeyVault(VaultName=${vault.name};SecretName=${smtpPasswordSecretName})' }
  // The review resource group and database persist across preview releases.
  { name: 'Seed__ReviewDemo', value: env == 'review' ? 'true' : 'false' }
  // The app's managed identity signs in to PostgreSQL; no database password exists (§21).
  { name: 'Db__UseManagedIdentity', value: 'true' }
  { name: 'ConnectionStrings__Hub', value: 'Host=${db.properties.fullyQualifiedDomainName};Database=hub;Username=${name}-app;Ssl Mode=Require' }
  { name: 'Csp__ConnectSrc', value: 'https://*.applicationinsights.azure.com' }
]

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
    network: { publicNetworkAccess: privateDatabase ? 'Disabled' : 'Enabled' }
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
    virtualNetworkSubnetId: appSubnetResourceId
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
      appSettings: appSettings
    }
  }
}

resource dbPrivateEndpoint 'Microsoft.Network/privateEndpoints@2024-05-01' = if (privateDatabase) {
  name: '${name}-pg-private-endpoint'
  location: location
  properties: {
    subnet: { id: dbPrivateEndpointSubnetResourceId }
    privateLinkServiceConnections: [{
      name: '${name}-pg-connection'
      properties: { privateLinkServiceId: db.id, groupIds: ['postgresqlServer'] }
    }]
  }
}

resource dbPrivateDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-05-01' = if (privateDatabase) {
  parent: dbPrivateEndpoint
  name: 'default'
  properties: { privateDnsZoneConfigs: [{ name: 'postgresql', properties: { privateDnsZoneId: dbPrivateDnsZoneResourceId } }] }
}

resource appHostnameBinding 'Microsoft.Web/sites/hostNameBindings@2023-12-01' = if (!empty(appHostname) && !empty(appCertificateThumbprint)) {
  parent: app
  name: appHostname
  properties: {
    siteName: app.name
    hostNameType: 'Verified'
    sslState: 'SniEnabled'
    thumbprint: appCertificateThumbprint
  }
}

resource appSlot 'Microsoft.Web/sites/slots@2023-12-01' = if (enableDeploymentSlot) {
  parent: app
  name: 'staging'
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
      appSettings: appSettings
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

resource operatorVaultReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(operatorGroupObjectId)) {
  name: guid(vault.id, operatorGroupObjectId, 'secrets-officer')
  scope: vault
  properties: {
    principalId: operatorGroupObjectId
    principalType: 'Group'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7')
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
  { key: 'availability', title: 'Hub health availability below 99 %', severity: 1, window: 'PT15M', query: 'availabilityResults | summarize availability = avg(todouble(success)) | where availability < 0.99', op: 'GreaterThan', threshold: 0 }
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

resource availabilityTest 'Microsoft.Insights/webtests@2022-06-15' = if (alerting) {
  name: '${name}-health-availability'
  location: 'global'
  tags: { 'hidden-link:${app.id}': 'Resource' }
  kind: 'ping'
  properties: {
    Kind: 'ping'
    SyntheticMonitorId: '${name}-health-availability'
    Name: '${name} health'
    Frequency: 300
    Timeout: 30
    Enabled: true
    Locations: [{ Id: 'us-ca-sjc-azr' }]
    Request: { RequestUrl: 'https://${app.properties.defaultHostName}/health', HttpVerb: 'GET', ParseDependentRequests: false }
    ValidationRules: { ExpectedHttpStatusCode: 200, SSLCheck: true, SSLCertRemainingLifetimeCheck: 7 }
  }
}

resource dbCpuAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = if (alerting) {
  name: '${name}-db-cpu'
  location: 'global'
  properties: {
    description: 'PostgreSQL CPU is above 80 percent for 15 minutes.'
    severity: 2
    enabled: true
    scopes: [db.id]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [{
        criterionType: 'StaticThresholdCriterion'
        name: 'High CPU'
        metricNamespace: 'Microsoft.DBforPostgreSQL/flexibleServers'
        metricName: 'cpu_percent'
        operator: 'GreaterThan'
        threshold: 80
        timeAggregation: 'Average'
      }]
    }
    autoMitigate: true
    actions: [{ actionGroupId: operators.id }]
  }
}
