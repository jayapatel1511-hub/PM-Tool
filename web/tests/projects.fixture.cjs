// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */

/**
 * A permission verdict: refused with no reason unless given, as the server sends it for a person who may not do the thing.
 * @param {boolean} [ok]
 * @param {string | null} [reason]
 * @returns {Schemas['ProjectPermission']}
 */
const projectPermission = (ok = false, reason = null) => ({ ok, reason })

/**
 * The header's permissions: every verdict refused, no discipline lists, not the manager, no transitions and no restore verdict (it is sent only
 * while the show-deleted flag is on). A suite gives only the verdicts its checks read, as a verdict or as `true`/`false`.
 * @param {{ [K in keyof Schemas['ProjectPermissions']]?: Schemas['ProjectPermissions'][K] | boolean }} [p]
 * @returns {Schemas['ProjectPermissions']}
 */
const projectPermissions = (p = {}) => {
  const no = projectPermission()
  /** @type {Schemas['ProjectPermissions']} */
  const all = {
    edit: no, manageTeam: no, manageMilestones: no, healthOverride: no, comment: no, raiseRegister: no, runCoordination: no, createEvent: no, enterTime: no,
    proposeAllocation: no, snooze: no, manageExternalParties: no, reviewDates: no, captureWeeklyPlan: no, createSubmission: no,
    createTaskIn: [], createDeliverableIn: [], createBasisIn: [], assignBasisIn: [], leadOf: [], isPm: false, transitions: [], changeNumber: false, setVisibility: false, needsReason: false,
  }
  const merged = /** @type {Record<string, unknown>} */ (all)
  for (const [key, value] of Object.entries(p)) merged[key] = typeof value === 'boolean' && typeof merged[key] === 'object' && !Array.isArray(merged[key]) ? projectPermission(value) : value // a boolean stands for the verdict of that name
  return all
}

/**
 * The header's health: computed and reported Green with no override, no reasons and no evaluation unless given.
 * @param {Partial<Schemas['ProjectHealth']>} [h]
 * @returns {Schemas['ProjectHealth']}
 */
const projectHealth = (h = {}) => ({
  computed: 'Green', reported: 'Green', overrideActive: false, healthOverride: null, healthOverrideNote: null, overrideBy: null, healthOverrideAt: null, healthOverrideExpiresAt: null,
  reasons: [], inputs: null, evaluatedAt: null, ...h,
})

/**
 * A discipline on the project header: active, with no lead, as the server sends it.
 * @param {Pick<Schemas['ProjectDisciplineRow'], 'id' | 'name'> & Partial<Schemas['ProjectDisciplineRow']>} d
 * @returns {Schemas['ProjectDisciplineRow']}
 */
const projectDiscipline = (d) => ({ disciplineId: d.id, code: d.name.slice(0, 3).toUpperCase(), colour: '#2563eb', leadUserId: null, leadName: null, isActive: true, rowVersion: 1, ...d })

/**
 * GET /projects/{idOrNumber}, typed against the contract so a server rename of a field fails `npm run typecheck:tests`. An Active project the
 * person has no part in unless given: no links or disciplines, Green, no permissions, and every date and optional member sent as null. The
 * flag-gated `statusNote` and `latestPlanned` are left out, as the server leaves them out while their flags are off.
 * @param {Pick<Schemas['ProjectDetail'], 'id' | 'projectNumber' | 'name'> & Partial<Omit<Schemas['ProjectDetail'], 'health' | 'permissions'>> & {
 *   health?: Partial<Schemas['ProjectHealth']>, permissions?: Parameters<typeof projectPermissions>[0] }} r
 * @returns {Schemas['ProjectDetail']}
 */
const projectDetail = ({ health, permissions, ...r }) => ({
  clientId: '00000000-0000-0000-0000-0000000000c1', clientReference: null, projectManagerId: '00000000-0000-0000-0000-0000000000a1', pmName: 'Synthetic PM',
  officeId: '00000000-0000-0000-0000-0000000000f1', projectTypeId: null, description: null, location: null, status: 'Active', phaseId: null, startDate: null, targetCompletionDate: null,
  priority: 'Medium', visibility: 'Open', internalNotes: null, coordinationDay: null, allowViewerComments: true, lastCoordinationReviewedAt: null, lastCoordinationReviewedBy: null,
  statusChangedAt: null, activatedAt: null, completedAt: null, archivedAt: null, createdFromTemplateId: null, templateVersion: null, rowVersion: 1, templateName: null,
  client: 'Synthetic client', office: 'Synthetic office', phase: null, links: [], disciplines: [], progressPct: null, nextMilestone: null, nextSubmission: null, myRoles: [],
  follow: null, starred: false, setupChecklist: null, suggestArchive: false, editWindowDaysLeft: null, ...r,
  health: projectHealth(health), permissions: projectPermissions(permissions),
})

/**
 * One row of GET /projects: an Active project at no risk with nothing overdue, not starred and with no next milestone unless given.
 * @param {Pick<Schemas['ProjectRow'], 'id' | 'projectNumber' | 'name'> & Partial<Schemas['ProjectRow']>} r
 * @returns {Schemas['ProjectRow']}
 */
const projectRow = (r) => ({
  status: 'Active', priority: 'Medium', visibility: 'Open', startDate: null, targetCompletionDate: null, client: { id: '00000000-0000-0000-0000-0000000000c1', name: 'Synthetic client' },
  pm: { id: '00000000-0000-0000-0000-0000000000a1', displayName: 'Synthetic PM' }, officeId: '00000000-0000-0000-0000-0000000000f1', office: 'Synthetic office', phaseId: null, phase: null,
  projectTypeId: null, computedHealth: 'Green', reportedHealth: 'Green', healthReasons: [], healthOverrideNote: null, healthOverrideAt: null, overrideBy: null, overrideActive: false,
  progressPct: null, overdueTasks: 0, blockedTasks: 0, overdueDecisions: 0, highIssues: 0, attentionCritical: 0, attentionWarning: 0, nextMilestone: null, nextSubmission: null,
  myRoles: [], starred: false, lastActivityAt: null, ...r,
})

/**
 * GET /projects: the first page of the given rows, in a page of 200 unless given.
 * @param {Schemas['ProjectRow'][]} [items]
 * @param {Partial<Schemas['PageOfProjectRow']>} [page]
 * @returns {Schemas['PageOfProjectRow']}
 */
const projectList = (items = [], page = {}) => ({ items, page: 1, pageSize: 200, totalCount: items.length, sort: null, filters: null, ...page })

/**
 * GET /projects/{id}/closeout: nothing open unless given.
 * @param {Partial<Schemas['CloseoutView']>} [c]
 * @returns {Schemas['CloseoutView']}
 */
const closeoutView = (c = {}) => ({ status: 'Active', openTasks: 0, tasks: [], deliverables: [], decisions: [], openRegisterItems: 0, ...c })

/**
 * GET /projects/{id}/date-review: no window and no work to review unless given.
 * @param {Partial<Schemas['DateReviewView']>} [r]
 * @returns {Schemas['DateReviewView']}
 */
const dateReviewView = (r = {}) => ({ window: null, tasks: [], deliverables: [], ...r })

module.exports = { projectPermission, projectPermissions, projectHealth, projectDiscipline, projectDetail, projectRow, projectList, closeoutView, dateReviewView }
