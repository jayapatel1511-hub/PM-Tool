// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */
const common = require('./task-contribution.fixture.cjs')
const { taskPage } = require('./tasks.fixture.cjs')
const { ids, id, stamp } = common
/** @param {{work?:boolean, people?:boolean, small?:boolean, own?:boolean, readonly?:boolean}} [options] */
function world(options) {
  const source = common.world(options)
  let transitionAllowed = true
  /** @type {number | undefined} */
  let secondVersion
  /** @param {string} [taskId] @returns {Schemas['TaskDetailResponse']} */
  const detail = taskId => {
    const result = source.detail(taskId)
    const refusal = 'Synthetic current Task is read-only'
    const denied = { ok: false, reason: refusal }
    return { ...result, task: { ...result.task, ...(result.task.id === ids.secondTask && secondVersion !== undefined ? { rowVersion: secondVersion } : {}) },
      // The current Task read reflects a concurrent Read Only demotion; the earlier /me remains cached.
      collaborators: transitionAllowed ? result.collaborators : result.collaborators.map(person => person.id === ids.viewer && person.contribution
        ? { ...person, contribution: { ...person.contribution, canMark: false, canWithdraw: false, refusalReason: refusal } } : person),
      permissions: { ...result.permissions, ...(!transitionAllowed ? { edit: denied, assign: denied, setReviewer: denied, dueDate: denied, block: denied, delete: denied,
        restore: false, comment: false, dependencies: false, enterTime: false, authoriseStart: false, offerPickup: false } : {}),
        transitions: result.permissions.transitions.map(transition => transitionAllowed ? transition : { ...transition, allowed: false, reason: refusal }) } }
  }
  return { ...source, detail, tasks: () => taskPage([detail(ids.task).task, detail(ids.secondTask).task]),
    /** @param {boolean} allowed */
    transitionPermission: allowed => { transitionAllowed = allowed },
    /** @param {number} version */
    secondVersion: version => { secondVersion = version },
  }
}
/** Actual history cohort spans three pages and contains inactive and System actors. */
/** @type {Schemas['ActivityEntry'][]} */
const history = Array.from({ length: 205 }, (_, index) => ({ id: id(98200 + index), occurredAt: new Date(Date.parse(stamp) - index * 60000).toISOString(),
  actorId: index < 125 ? ids.pat : index < 204 ? ids.inactive : null, actorName: index < 125 ? 'Synthetic Pat Reviewer' : index < 204 ? 'Synthetic Former Designer' : 'System', actorType: index === 204 ? 'System' : 'User',
  projectId: ids.project, itemType: index === 203 ? 'TaskContribution' : 'Task', itemId: ids.task, itemKey: 'S16-T001', itemName: 'Synthetic section task', action: 'Updated', categories: ['date'],
  changes: [{ field: 'DueDate', old: '2026-11-19', new: '2026-11-20', oldLabel: '2026-11-19', newLabel: '2026-11-20' }], summary: 'Synthetic history event ' + (index + 1),
  reason: 'Synthetic history event ' + (index + 1), source: 'API', correlationId: id(98600 + index), snapshot: null, snapshotLabels: null }))
/** @type {Schemas['TaskHistoryActorChoice'][]} */
const actors = [{ id: ids.pat, displayName: 'Synthetic Pat Reviewer', isActive: true }, { id: ids.inactive, displayName: 'Synthetic Former Designer', isActive: false }]
/** @param {URLSearchParams} query @param {Schemas['ActivityEntry'][]} [cohort] @returns {Schemas['PageOfActivityEntry']} */
function historyPage(query, cohort = history) {
  const actor = query.get('actorId'), page = Number(query.get('page') ?? 1), size = Number(query.get('pageSize') ?? 100)
  const rows = actor ? cohort.filter(row => row.actorId === actor) : cohort
  return { items: rows.slice((page - 1) * size, page * size), page, pageSize: size, totalCount: rows.length }
}
module.exports = { ...common, world, history, actors, historyPage }
