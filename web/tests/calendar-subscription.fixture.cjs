// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */

/**
 * The Sync my calendar dialog's current state, typed against the contract so a server rename of a field fails `npm run typecheck:tests`.
 * @param {boolean} enabled
 * @returns {Schemas['SubscriptionState']}
 */
const subscriptionState = (enabled) => ({ enabled, rowVersion: enabled ? 2 : 0 })

/**
 * The create-or-rotate answer, which carries the private link once.
 * @param {string} url
 * @returns {Schemas['SubscriptionLink']}
 */
const subscriptionLink = (url) => ({ url, rowVersion: 2 })
module.exports = { subscriptionState, subscriptionLink }
