using '../main.bicep'
param env = 'review'
// Deploy in a dedicated review resource group with separate Entra registrations.
// Fill these values from the company tenant before deployment.
param entraTenantId = ''
param apiAudience = ''
param spaClientId = ''
param apiScope = ''
param dbAdminGroupObjectId = ''
param dbAdminGroupName = ''
