// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */

/**
 * One person's expand row typed against the contract, so a server rename of a field fails `npm run typecheck:tests`.
 * The first task is dated and estimated; the second has neither, so its nulls reach the page as the server sends them.
 * @param {string[]} weeks
 * @param {{ id: string, projectNumber: string, name: string }} project
 * @returns {Schemas['PersonTaskLoad']}
 */
const personTasks = (weeks, project) => ({
  weeks,
  projects: [{ id: project.id, projectNumber: project.projectNumber, name: project.name, tasks: [
    { id: '00000000-0000-0000-0000-000000000091', key: 'P-DEMO-T0091', name: 'Synthetic dated task', status: 'In Progress', dueDate: '2026-10-09', estimatedHours: 16, progressPct: 25,
      remaining: 12, overdue: false, noDueDate: 0, rowVersion: 3, weeks: weeks.map((_, i) => (i === 0 ? 12 : 0)), canReassign: false },
    { id: '00000000-0000-0000-0000-000000000092', key: 'P-DEMO-T0092', name: 'Synthetic open task', status: 'Not Started', dueDate: null, estimatedHours: null, progressPct: 0,
      remaining: null, overdue: false, noDueDate: 0, rowVersion: 1, weeks: weeks.map(() => 0), canReassign: false },
  ] }],
})

/**
 * The capacity override's answer: the hours now stored, or null when the override was cleared.
 * @param {string} id
 * @param {number | null} hours
 * @returns {Schemas['CapacitySet']}
 */
const capacitySet = (id, hours) => ({ id, weeklyCapacityHours: hours })
module.exports = { personTasks, capacitySet }
