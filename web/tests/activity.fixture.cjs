// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']['ActivityEntry']} ActivityEntry */
/**
 * Five edits of one item, newest first, typed against the contract so a server rename fails `npm run typecheck:tests`.
 * @param {(n: number) => string} id
 * @param {{ actorId: string, itemId: string, itemKey: string, itemName: string }} item
 * @returns {import('../src/gen/api').components['schemas']['PageOfActivityEntry']}
 */
const activityPage = (id, item) => {
  /** @type {ActivityEntry[]} */
  const items = ['12:10', '12:08', '12:04', '12:00', '11:49'].map((time, i) => ({
    id: id(40 + i), occurredAt: `2026-10-18T${time}:00Z`, actorId: item.actorId, actorName: 'Synthetic PM', actorType: 'User',
    projectId: null, itemType: 'Task', itemId: item.itemId, itemKey: item.itemKey, itemName: item.itemName, action: 'Updated', categories: ['dates'],
    changes: [{ field: 'Name', old: `Before ${i}`, new: `After ${i}`, oldLabel: `Before ${i}`, newLabel: `After ${i}` }],
    summary: `Synthetic change ${i}`, reason: `Synthetic reason ${i}`, source: 'Web', correlationId: null, snapshot: null, snapshotLabels: null,
  }))
  return { items, page: 1, pageSize: 50, totalCount: items.length }
}
module.exports = { activityPage }
