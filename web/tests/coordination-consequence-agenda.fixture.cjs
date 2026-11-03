// @ts-check
/** Synthetic S38 wire contracts; no readiness, receiving relation or operational data is inferred. */
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */
const { coordinationProjection } = require('./coordination.fixture.cjs')
const { weeklyCoordination } = require('./dashboard.fixture.cjs')
const { handoffRow, handoffPage, handoffDetail, handoffOptions } = require('./handoffs.fixture.cjs')
const { taskRow } = require('./tasks.fixture.cjs')
/** @param {number} n */
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
/** @param {Schemas['ProjectDetail']} project @param {string} actor */
function coordinationConsequenceFixture(project, actor) {
  const h = id(59001), c = id(59002), proposed = id(59003), removal = id(59004)
  const from = '2026-11-09', to = '2026-11-15', discipline = project.disciplines[0].id
  /** @type {Schemas['RecordContextPerson']} */
  const performer = { id: actor, name: 'Synthetic performer', state: 'Active', kind: 'User' }
  /** @type {Schemas['RecordContextPerson']} */
  const removalOwner = { id: removal, name: 'Synthetic removal owner', state: 'Active', kind: 'User' }
  /** @type {Schemas['TaskQueriesRow'][]} */
  const tasks = Array.from({ length: 57 }, (_, n) => taskRow({ id: id(59200 + n), key: `G59-T${String(n + 1).padStart(3, '0')}`, name: `Synthetic linked task ${n + 1}`,
    projectId: project.id, projectDisciplineId: discipline, dueDate: n === 0 ? '2026-11-10' : n === 1 ? null : '2026-11-13', assigneeId: actor, assigneeName: performer.name }))
  /** @param {boolean} [large] @returns {Schemas['HandoffReadinessLinks']} */
  const summary = (large = false) => ({ state: 'Known', scope: 'ProjectOpenReadinessLinks', taskCount: large ? 57 : 2, directTaskCount: 1,
    constraintLinkedTaskCount: large ? 56 : 1, datedTaskCount: large ? 56 : 1, firstRecordedDueDate: '2026-11-10', hasPromisedDateMismatch: true,
    isOverdue: false, isHistoricalMismatch: true, directReceiptSatisfied: true })
  /** @param {boolean} [large] */
  const handoff = (large = false) => handoffRow({ id: h, key: 'G59-H001', title: 'Synthetic accepted handoff', status: 'Accepted', targetTaskId: tasks[0].id,
    targetKey: tasks[0].key, neededBy: '2026-11-07', promisedBy: '2026-11-10', sendingOwnerId: actor, receivingOwnerId: removal,
    sendingDisciplineId: discipline, receivingDisciplineId: discipline, readinessLinks: summary(large) })
  /** @param {number} page @param {boolean} [large] @returns {Schemas['HandoffLinkedTaskPage']} */
  const linked = (page, large = false) => ({ projectId: project.id, handoffId: h, summary: summary(large), tasks: {
    page, pageSize: 50, totalCount: large ? 57 : 2, items: tasks.slice((page - 1) * 50, Math.min(page * 50, large ? 57 : 2)).map((task, n) => ({
      id: task.id, key: task.key, name: task.name, status: task.status, dueDate: task.dueDate,
      relationships: page === 1 && n === 0 ? [{ relationship: 'DirectTarget', constraintId: null, constraintKey: null, constraintState: null, constraintNeededBy: null, removalOwner: null, affectedOwner: null }]
        : [{ relationship: 'Constraint', constraintId: page === 1 && n === 1 ? c : id(59700 + (page - 1) * 50 + n),
          constraintKey: page === 1 && n === 1 ? 'G59-C001' : `G59-CT${(page - 1) * 50 + n}`, constraintState: 'Open',
          constraintNeededBy: page === 1 && n === 1 ? '2026-11-08' : '2026-11-18', removalOwner, affectedOwner: performer }],
    })),
  } })
  /** @param {number} cp @param {number} pp @returns {Schemas['WeeklyReadinessAgenda']} */
  const agenda = (cp = 1, pp = 1) => ({ projectId: project.id, from, to, disciplineId: null, constraintScope: 'NeededByThroughEndIncludingOverdue',
    constraints: { page: cp, pageSize: 50, totalCount: 51, items: cp === 1 ? [{ id: c, key: 'G59-C001', targetType: 'Task', targetId: tasks[1].id, targetState: 'Available', targetKey: tasks[1].key,
      description: 'Synthetic overdue input constraint', state: 'Open', neededBy: '2026-11-08', removalOwner, affectedOwner: performer },
    ...Array.from({ length: 49 }, (_, n) => ({ id: id(59500 + n), key: `G59-C${n + 2}`, targetType: 'Task', targetId: tasks[1].id, targetState: 'Available', targetKey: tasks[1].key,
      description: `Synthetic meeting constraint ${n + 2}`, state: 'Resolution Proposed', neededBy: from, removalOwner, affectedOwner: performer }))]
      : [{ id: id(59600), key: 'G59-C051', targetType: 'Task', targetId: tasks[1].id, targetState: 'Unavailable', targetKey: null,
        description: 'Synthetic last constraint', state: 'Open', neededBy: to, removalOwner, affectedOwner: performer }] },
    proposals: { page: pp, pageSize: 50, totalCount: 1, items: pp === 1 ? [{ id: proposed, key: 'G59-P001', targetType: 'Task', targetId: tasks[0].id,
      targetState: 'Available', targetKey: tasks[0].key, intendedOutput: 'Synthetic proposed layout', targetDate: '2026-11-12', weekStart: from, performer, state: 'Proposed' }] : [] },
  })
  const weekly = () => { const base = weeklyCoordination(); return { ...base, window: { ...base.window, thisWeek: { from, to } } } }
  /** @param {boolean} [large] */
  const projection = (large = false) => coordinationProjection({ outgoing: [{ id: h, key: 'G59-H001', title: 'Synthetic accepted handoff', status: 'Accepted', neededBy: '2026-11-07', promisedBy: '2026-11-10',
    targetTaskId: tasks[0].id, targetDeliverableId: null, sendingOwnerId: actor, receivingOwnerId: removal, sendingDisciplineId: discipline, receivingDisciplineId: discipline, readinessLinks: summary(large) }],
  outgoingTotal: 1, outgoingPage: 1, handoffPageSize: 4, page: 1, pageSize: 4 })
  return { h, c, proposed, from, to, tasks, summary, linked, agenda, weekly, projection, handoff,
    list: () => handoffPage([handoff()]), detail: (large = false) => handoffDetail({ row: handoff(large) }), options: () => handoffOptions({ canCreate: false }) }
}
module.exports = { coordinationConsequenceFixture }
