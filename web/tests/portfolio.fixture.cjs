// @ts-check
/**
 * Portfolio answers typed against the contract so a server rename of a tile or of the admin strip fails `npm run typecheck:tests`.
 * The rows are the project list's own and stay untyped (`unknown[]`) in the contract until the Projects list row is a record.
 * @returns {import('../src/gen/api').components['schemas']['PortfolioView']}
 */
const emptyPortfolio = () => ({ ownOnly: false, tiles: { active: 0, red: 0, yellow: 0, green: 0, submissions14: 0, overdueDecisions: 0 }, projects: [] })

/** @type {import('../src/gen/api').components['schemas']['AdminReviewStrip']} */
const lastReview = { reviewedAt: '2026-10-14T15:00:00Z', checks: [{ key: 'inactiveOnTeams', title: 'Inactive users still on teams', count: 2 }, { key: 'noManager', title: 'Users without a manager', count: 1 }] }
module.exports = { emptyPortfolio, lastReview }
