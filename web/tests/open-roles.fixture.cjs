// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']['OpenRoleRow']} OpenRole */
/** @type {OpenRole} */
const contractExample = {
  id: '00000000-0000-0000-0000-000000000001', projectId: '00000000-0000-0000-0000-000000000010',
  roleLabel: 'Synthetic contract role', projectDisciplineId: null, disciplineName: null,
  fromDate: '2026-10-19', throughDate: '2026-10-23', plannedHours: 20, status: 'Open',
  filledAllocationId: null, note: null, rowVersion: 1, canManage: true,
}
/** @type {OpenRole[]} */
const openRoles = []
module.exports = { openRoles, contractExample }
