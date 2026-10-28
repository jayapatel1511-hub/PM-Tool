// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */

/**
 * A dashboard figure and the query string of the list it opens.
 * @param {number} [value]
 * @param {string} [link]
 * @returns {Schemas['DashboardCount']}
 */
const dashboardCount = (value = 0, link = '') => ({ value, link })

/**
 * One discipline row of the dashboard, typed against the contract so a server rename of a field fails `npm run typecheck:tests`.
 * A discipline with no lead and nothing due has its lead and next-due fields sent as null, as the server does.
 * @param {Pick<Schemas['DashboardDiscipline'], 'disciplineId' | 'name'> & Partial<Schemas['DashboardDiscipline']>} r
 * @returns {Schemas['DashboardDiscipline']}
 */
const dashboardDiscipline = (r) => ({
  leadId: null, health: 'Green', reasons: [], open: 0, overdue: 0, blocked: 0, waiting: 0, deliverablesDue14: 0, sortOrder: 0,
  nextDueId: null, nextDueKey: null, nextDueName: null, nextDueDate: null, nextDueType: null, ...r,
})

/**
 * The project dashboard with every count at zero and every list empty; the fields given replace the defaults.
 * `disciplineStatus` and `disciplineBacklog` (and `tasks.backlog`) are left out, as the server leaves them out while their flags are off.
 * @param {Partial<Schemas['DashboardView']>} [r]
 * @returns {Schemas['DashboardView']}
 */
const dashboardView = (r = {}) => ({
  milestones: [],
  tasks: { total: dashboardCount(), complete: dashboardCount(), inProgress: dashboardCount(), review: dashboardCount(), overdue: dashboardCount(), blocked: dashboardCount(),
    waiting: dashboardCount(), unassigned: dashboardCount() },
  deliverables: { upcoming: dashboardCount(), atRisk: dashboardCount(), issued: dashboardCount(), total: dashboardCount() },
  decisions: { pending: dashboardCount(), overdue: dashboardCount() },
  issues: { open: 0, high: 0 }, risks: { high: 0 },
  registers: { issuesOpen: dashboardCount(), issuesHigh: dashboardCount(), risksHigh: dashboardCount() },
  disciplines: [], disciplineDue14: {},
  dueThisWeek: { items: [], total: 0, link: '' }, blocked: { items: [], total: 0, link: '' },
  activity: [], state: null, ...r,
})

/**
 * The Weekly Coordination agenda with every section empty; the fields given replace the defaults.
 * @param {Partial<Schemas['WeeklyCoordination']>} [r]
 * @returns {Schemas['WeeklyCoordination']}
 */
const weeklyCoordination = (r = {}) => ({
  window: { thisWeek: { from: '2026-10-01', to: '2026-10-07' }, nextWeek: { from: '2026-10-08', to: '2026-10-14' }, since: '2026-09-01T00:00:00Z',
    lastCoordinationReviewedAt: null, reviewedBy: null, canMarkReviewed: false },
  headline: { computedHealth: 'Green', healthReasons: null, overdueTasks: dashboardCount(0, '#'), blockedTasks: dashboardCount(0, '#'), decisionsOverdue: 0,
    deliverablesAtRisk: dashboardCount(0, '#'), nextMilestone: null },
  milestones: [], deliverables: [], decisions: [], blocked: [], overdue: [], dueThisWeek: [], disciplines: [], issues: [], risks: [], waiting: [],
  completed: { tasks: [], deliverables: [], decisions: [] }, upcoming: { tasks: [], deliverables: [] }, held: { tasks: [], deliverables: [] }, ...r,
})

module.exports = { dashboardCount, dashboardDiscipline, dashboardView, weeklyCoordination }
