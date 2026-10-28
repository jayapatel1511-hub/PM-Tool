// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */
/**
 * One project's timeline answer typed against the contract, so a server rename of a field fails `npm run typecheck:tests`.
 * Handoffs and each task's chain ids are in because the project's own read carries them with timeline_handoffs_chain on.
 * @param {{ discipline: string, task: string, deliverable: string, milestone: string, handoff: string }} id
 * @returns {Schemas['TimelineData']}
 */
const projectTimeline = (id) => ({
  latestPlanned: { date: '2026-11-05', itemKey: 'SYN-046-D001', targetDate: '2026-10-30', rule: 'Working days' },
  handoffs: [{ id: id.handoff, key: 'SYN-046-H001', title: 'Civil to Electrical', status: 'Submitted', sendingDisciplineId: id.discipline, receivingDisciplineId: id.discipline, neededBy: '2026-10-25', promisedBy: '2026-10-27', dateMismatch: true, isOverdue: true, daysOverdue: 3 }],
  permissions: { manageMilestones: false, needsReason: false },
  tasks: [{ id: id.task, key: 'SYN-046-T001', name: 'Synthetic task', deliverableId: id.deliverable, projectDisciplineId: id.discipline, milestoneId: id.milestone, startDate: '2026-10-20', dueDate: '2026-10-30', originalStartDate: null, originalDueDate: null,
    status: 'Not Started', progressPct: 0, assignee: 'Synthetic PM', rowVersion: 2, createdOn: '2026-10-18', isOverdue: false, canEditDates: false, dueNeedsReason: false, slipShown: true, affectedMilestoneIds: [id.milestone] }],
  dependencies: [],
  deliverableLinks: [],
  deliverables: [{ id: id.deliverable, key: 'SYN-046-D001', name: 'Synthetic deliverable', startDate: '2026-10-20', dueDate: '2026-10-30', originalStartDate: null, originalDueDate: null, status: 'In Progress', rowVersion: 2, createdOn: '2026-10-18', progressPct: 0, isOverdue: false,
    canEditDates: true, slipShown: true, baselineStartDate: '2026-10-20', baselineDueDate: '2026-10-28', baselineSlipShown: true }],
  milestones: [{ id: id.milestone, key: 'SYN-046-M01', name: 'Synthetic review', date: '2026-11-01', isComplete: false, status: null, originalDate: '2026-10-29', slipShown: true, baselineDate: '2026-10-29', baselineSlipShown: true }],
})

/**
 * The cross-project answer for one project: the same data without the handoffs and chain ids, which only the project's own read carries.
 * @param {{ id: string, projectNumber: string, name: string, status: string, startDate?: string | null, targetCompletionDate?: string | null }} project
 * @param {Schemas['TimelineData']} data
 * @returns {Schemas['TimelineGantt']}
 */
const ganttPage = (project, data) => ({
  projects: [{
    project: { id: project.id, projectNumber: project.projectNumber, name: project.name, status: project.status, startDate: project.startDate ?? null, targetCompletionDate: project.targetCompletionDate ?? null },
    data: { ...data, handoffs: undefined, tasks: data.tasks.map(({ affectedMilestoneIds, ...task }) => task) },
  }],
  truncated: false,
  max: 25,
})
module.exports = { projectTimeline, ganttPage }
