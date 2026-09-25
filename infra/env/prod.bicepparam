using '../main.bicep'
param env = 'prod'
// Fill in from the environment's Entra app registrations (separate per environment, §21).
param entraTenantId = ''
param apiAudience = ''
param spaClientId = ''
param apiScope = ''
param dbAdminGroupObjectId = ''
param dbAdminGroupName = ''
