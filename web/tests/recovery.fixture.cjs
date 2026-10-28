// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']['DeletedItem']} DeletedItem */
/**
 * The Show deleted list for tasks, typed against the contract so a server rename of a field fails `npm run typecheck:tests`:
 * one task that can be restored and one whose deliverable is deleted too. Nothing is listed once the first is restored.
 * @param {{ deleted: string, parent: string, restored: boolean }} p
 * @returns {import('../src/gen/api').components['schemas']['PageOfDeletedItem']}
 */
const deletedTasks = (p) => {
  /** @type {DeletedItem[]} */
  const items = p.restored ? [] : [
    { id: p.deleted, key: 'SYN-RCV-T001', name: 'Synthetic deleted task', discipline: 'Civil', parentKey: 'SYN-RCV-D001', status: 'Not Started', deletedAt: '2026-10-18T12:00:00Z', deletedBy: 'Synthetic PM', rowVersion: 4, restoreBlocked: null },
    { id: p.parent, key: 'SYN-RCV-T002', name: 'Synthetic child', discipline: 'Civil', parentKey: 'SYN-RCV-D002', status: 'Not Started', deletedAt: '2026-10-18T11:00:00Z', deletedBy: 'Synthetic PM', rowVersion: 2, restoreBlocked: 'Restore SYN-RCV-D002 first' },
  ]
  return { items, page: 1, pageSize: 200, totalCount: items.length }
}
module.exports = { deletedTasks }
