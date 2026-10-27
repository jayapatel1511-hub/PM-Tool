// @ts-check
/**
 * The Copy / Publish week preview and apply answers, typed against the contract so a server rename of a candidate or result field
 * fails `npm run typecheck:tests`.
 * @param {{ operation: string, week: string, entryId: string, personId: string }} p
 * @returns {import('../src/gen/api').components['schemas']['WeekPreview']}
 */
const weekPreview = (p) => ({
  operation: p.operation, week: p.week, snapshot: 'synthetic-snapshot',
  entries: [{ id: p.entryId, rowVersion: 4, personId: p.personId, personName: 'Alex Engineer', label: 'Synthetic weekly allocation', hoursPerWeek: 8, confidence: 'Possible', visibility: 'Draft', warnings: ['ProvisionalCapacity'] }],
})

/**
 * @param {{ week: string, entryId: string, personId: string }} p
 * @returns {import('../src/gen/api').components['schemas']['WeekApplied']}
 */
const weekApplied = (p) => ({ count: 1, items: [{ id: p.entryId, personId: p.personId, rowVersion: 5 }], week: p.week })
module.exports = { weekPreview, weekApplied }
