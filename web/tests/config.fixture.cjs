// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */

/**
 * The public sign-in configuration the SPA reads before anyone is signed in, typed against the contract so a server rename of a
 * field fails `npm run typecheck:tests`. The default is Development with no providers and the blank Entra block that
 * appsettings.json gives, which is what the real API answers.
 * @param {Partial<Schemas['ClientConfig']>} [c]
 * @returns {Schemas['ClientConfig']}
 */
const clientConfig = (c = {}) => ({ authMode: 'Development', providers: [], entra: { clientId: '', tenantId: '', apiScope: '' }, ...c })
module.exports = { clientConfig }
