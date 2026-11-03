// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */
const { meView } = require('./me.fixture.cjs')
const { taskRow, taskPage } = require('./tasks.fixture.cjs')
const { projectDetail, projectDiscipline, projectList, projectRow } = require('./projects.fixture.cjs')
const { deliverableRow } = require('./deliverables.fixture.cjs')
/** @param {number} n */
const id = n => '00000000-0000-0000-0000-' + String(n).padStart(12, '0')
// Same synthetic identities and detail fields as the original scenario, using data-only builders.
const originalProject = projectDetail({ id: id(10), projectNumber: 'P-MOCK', name: 'Synthetic live project', pmName: 'Alex Chen',
  disciplines: [projectDiscipline({ id: id(11), disciplineId: id(12), name: 'Civil' })], permissions: { edit: true } })
const me = meView({ id: id(2), displayName: 'Alex Chen', email: 'alex@hub.test', systemRoles: ['StandardUser'], capabilities: { createTask: true, workload: true, admin: true },
  settings: { today: '2026-10-03', restrictedProjectsEnabled: true }, flags: { live_events: true, live_project_stamp: true } })
const projects = [originalProject, ...[1, 2].map(n => ({ ...originalProject, id: id(9100 + n), projectNumber: 'P-BULK-' + n, name: 'Synthetic bulk project ' + n }))]
const departing = id(9800)
const people = [{ id: id(9801), displayName: 'Synthetic destination performer', email: 'destination@example.test', isActive: true, onProject: true }]
/** @param {boolean} [on] */
const actor = (on = true) => meView({ id: me.id, displayName: 'Synthetic bulk lead', email: 'bulk-lead@example.test', capabilities: { createTask: true, staff: true }, flags: { work_screens: on }, settings: { today: '2026-11-14' } })
function state() {
  const rows = Array.from({ length: 202 }, (_, index) => {
    const p = projects[index < 6 ? index % 3 : index === 101 ? 1 : 0]
    return taskRow({ id: id(9200 + index), projectId: p.id, projectNumber: p.projectNumber, projectName: p.name, key: 'BULK-' + (index + 1), name: 'Synthetic bulk task ' + (index + 1),
      assigneeId: me.id, assigneeName: 'Synthetic performer', startDate: '2026-11-20', dueDate: '2026-11-25', rowVersion: 0, can: { assign: index !== 100 && index !== 101, edit: index !== 100 && index !== 101, changeDue: index !== 100 && index !== 101, block: false } })
  })
  rows[7].assigneeId = departing
  rows[8].assigneeId = departing; rows[8].status = 'Complete'; rows[8].progressPct = 100
  rows[20].priority = 'Low'
  return { rows, hidden: new Set(), applied: 0, commands: 0 }
}
/** @param {ReturnType<typeof state>} source @param {URLSearchParams} query */
function page(source, query) {
  let rows = source.rows.filter(row => !source.hidden.has(row.id))
  if (query.get('priority')) rows = rows.filter(row => row.priority === query.get('priority'))
  const term = query.get('q'); if (term) rows = rows.filter(row => row.name.toLowerCase().includes(term.toLowerCase()))
  if (query.get('assigneeId')) rows = rows.filter(row => row.assigneeId === query.get('assigneeId'))
  if (query.get('open') === 'true') rows = rows.filter(row => row.status !== 'Complete' && row.status !== 'Cancelled')
  if (query.get('projectId')) rows = rows.filter(row => row.projectId === query.get('projectId'))
  const page = Number(query.get('page') ?? 1), size = Number(query.get('pageSize') ?? 100)
  return taskPage(rows.slice((page - 1) * size, page * size), { totalCount: rows.length, page, pageSize: size })
}
/** @param {string} project */
function deliverables(project) { return [0, 1].map(index => deliverableRow({ id: id(9400 + projects.findIndex(p => p.id === project) * 10 + index), projectId: project,
  key: 'DEL-BULK-' + (index + 1), name: 'Synthetic local deliverable ' + (index + 1), projectDisciplineId: originalProject.disciplines[0].id })) }
/** @param {ReturnType<typeof state>} source @param {Schemas['TaskBulkIntent']} intent @param {boolean} [apply] */
function resultRows(source, intent, apply = false) {
  /** @type {Schemas['TaskBulkRow'][]} */
  const out = intent.items.map((item, index) => {
    const row = source.rows.find(row => row.id === item.taskId && row.projectId === item.projectId)
    if (!row || source.hidden.has(item.taskId)) return { index, outcome: 'unavailable' }
    if (row.can?.edit === false || row.rowVersion !== item.rowVersion) return { index, outcome: 'refused', taskId: row.id, projectId: row.projectId, key: row.key,
      code: row.rowVersion !== item.rowVersion ? 'concurrency_conflict' : 'forbidden', reason: row.rowVersion !== item.rowVersion ? 'Synthetic task changed since selection' : 'Synthetic lead cannot edit this discipline', rowVersion: row.rowVersion }
    if (apply) {
      if (intent.operation === 'assign') { const params = /** @type {Schemas['TaskBulkParametersTaskBulkAssignParameters']} */ (intent.params); row.assigneeId = params.assigneeId; row.assigneeName = params.assigneeId ? people.find(person => person.id === params.assigneeId)?.displayName ?? 'Synthetic assigned performer' : null }
      if (intent.operation === 'setDueDate') row.dueDate = /** @type {Schemas['TaskBulkParametersTaskBulkDueDateParameters']} */ (intent.params).dueDate
      if (intent.operation === 'setPriority') row.priority = /** @type {Schemas['TaskBulkParametersTaskBulkPriorityParameters']} */ (intent.params).priority
      if (intent.operation === 'shiftDueDates') {
        const params = /** @type {Schemas['TaskBulkParametersTaskBulkShiftDatesParameters']} */ (intent.params)
        row.startDate = shifted(row.startDate, params.days); row.dueDate = shifted(row.dueDate, params.days)
      }
      if (intent.operation === 'setDeliverable') {
        const params = /** @type {Schemas['TaskBulkParametersTaskBulkDeliverableParameters']} */ (intent.params), target = params.targets.find(target => target.projectId === row.projectId)
        if (!target || target.deliverableId && !deliverables(row.projectId).some(choice => choice.id === target.deliverableId)) throw new Error('Missing or foreign explicit local deliverable target')
        row.deliverableId = target.deliverableId; row.milestoneId = null
      }
      if (intent.operation === 'hold' || intent.operation === 'cancel') row.status = intent.operation === 'hold' ? 'On Hold' : 'Cancelled'
      row.rowVersion++; source.applied++
    }
    return { index, outcome: apply ? 'changed' : 'eligible', taskId: row.id, projectId: row.projectId, key: row.key, rowVersion: row.rowVersion, ...(apply ? { activityIds: [id(9500 + source.applied)] } : {}) }
  })
  return out
}
/** @param {Schemas['TaskBulkRow'][]} rows */
const counts = rows => ({ changed: rows.filter(row => row.outcome === 'changed').length, unchanged: rows.filter(row => row.outcome === 'unchanged').length, refused: rows.filter(row => row.outcome === 'refused').length, unavailable: rows.filter(row => row.outcome === 'unavailable').length })
/** @param {string|null} value @param {number} days */
function shifted(value, days) { return value ? new Date(Date.parse(value + 'T00:00:00Z') + days * 86400000).toISOString().slice(0, 10) : null }
/** @type {Schemas['OpenWorkView']} */
const openWork = { person: { id: departing, displayName: 'Synthetic departing performer', isActive: false, email: 'departing@example.test' }, items: [
  { kind: 'TaskAssignee', id: id(9207), key: 'BULK-8', name: 'Synthetic bulk task 8', status: 'Not Started', dueDate: '2026-11-25', projectId: projects[0].id, projectNumber: projects[0].projectNumber, projectName: projects[0].name },
  { kind: 'DeliverableOwner', id: id(9900), key: 'DEPART-1', name: 'Synthetic departure drawing', status: 'Not Started', dueDate: null, projectId: projects[0].id, projectNumber: projects[0].projectNumber, projectName: projects[0].name },
] }
/** @param {Schemas['ProjectDetail']} p @returns {Schemas['ProjectRow']} */
const summary = p => projectRow({ id: p.id, projectNumber: p.projectNumber, name: p.name, status: p.status, priority: p.priority, visibility: p.visibility,
  startDate: p.startDate, targetCompletionDate: p.targetCompletionDate, client: { id: p.clientId, name: p.client },
  pm: { id: p.projectManagerId, displayName: p.pmName }, officeId: p.officeId, office: p.office, phaseId: p.phaseId, phase: p.phase,
  projectTypeId: p.projectTypeId, computedHealth: p.health.computed, reportedHealth: p.health.reported })
module.exports = { id, actor, state, page, projects, departing, people, openWork, projectList: () => projectList(projects.map(summary)), deliverables, resultRows, counts }
