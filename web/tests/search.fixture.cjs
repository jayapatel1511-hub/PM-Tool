// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */

/**
 * One planning-entry hit of the global search, typed against the contract so a server rename of a field fails `npm run typecheck:tests`.
 * @param {Pick<Schemas['SearchPlanningHit'], 'id' | 'name' | 'personId' | 'startWeek'> & Partial<Schemas['SearchPlanningHit']>} r
 * @returns {Schemas['SearchPlanningHit']}
 */
const planningHit = (r) => ({ status: 'Published', dueDate: r.startWeek, ...r })

/**
 * The search answer for the groups given: each group's count is its row count, and no key was an exact match.
 * @param {string} q
 * @param {Schemas['SearchGroups']} groups
 * @returns {Schemas['SearchResponse']}
 */
const searchResult = (q, groups) => ({ q, exact: null, groups, counts: Object.fromEntries(Object.entries(groups).map(([group, rows]) => [group, rows?.length ?? 0])) })
module.exports = { planningHit, searchResult }
