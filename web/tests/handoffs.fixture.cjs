// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */

const none = '00000000-0000-4000-8000-000000000000'
const stamp = '2026-09-27T02:00:00Z'

/**
 * One handoff as the register and the detail panel read it, typed against the contract so a server rename of a field fails `npm run typecheck:tests`.
 * @param {Pick<Schemas['HandoffRow'], 'id' | 'key' | 'title'> & Partial<Schemas['HandoffRow']>} r
 * @returns {Schemas['HandoffRow']}
 */
const handoffRow = (r) => ({
  status: 'Draft', rowVersion: 0, sourceDeliverableId: none, sourceRowVersion: 0, declaredRevision: 'A', sourceUrl: 'https://example.test/survey-A.pdf',
  sendingDisciplineId: none, receivingDisciplineId: none, sendingOwnerId: none, receivingOwnerId: none, targetTaskId: null, targetDeliverableId: null,
  intendedUse: 'Set the service alignment', acceptanceCriteria: 'The survey covers the corridor', neededBy: '2026-09-29', promisedBy: null,
  currentRevisionId: null, incorporatedRevisionId: null, senderName: null, receiverName: null, sourceKey: null, targetKey: null,
  ownersAvailable: true, sourceChanged: false, dateMismatch: false, isOverdue: false, ...r,
})

/**
 * One page of the register. The page and size are the request's, as the server echoes them.
 * @param {Schemas['HandoffRow'][]} items
 * @param {{ total?: number, page?: number, pageSize?: number }} [paging]
 * @returns {Schemas['PageOfHandoffRow']}
 */
const handoffPage = (items, paging = {}) => ({ items, page: paging.page ?? 1, pageSize: paging.pageSize ?? 50, totalCount: paging.total ?? items.length })

/**
 * What the create form chooses from.
 * @param {Partial<Schemas['HandoffOptions']>} [r]
 * @returns {Schemas['HandoffOptions']}
 */
const handoffOptions = (r = {}) => ({ sources: [], disciplines: [], people: [], tasks: [], canCreate: true, ...r })

/**
 * @param {Pick<Schemas['HandoffSource'], 'id' | 'key' | 'name' | 'projectDisciplineId'> & Partial<Schemas['HandoffSource']>} r
 * @returns {Schemas['HandoffSource']}
 */
const handoffSource = (r) => ({ ownerId: null, revision: null, transmittalUrl: null, rowVersion: 0, dueDate: null, ...r })

/**
 * @param {Pick<Schemas['HandoffTask'], 'id' | 'key' | 'name' | 'projectDisciplineId'> & Partial<Schemas['HandoffTask']>} r
 * @returns {Schemas['HandoffTask']}
 */
const handoffTask = (r) => ({ ownerId: null, dueDate: null, ...r })

/**
 * What this actor may do: allowed with no reason, or refused with the reason the server writes.
 * @param {boolean} [ok]
 * @param {string | null} [reason]
 * @returns {Schemas['HandoffPermission']}
 */
const handoffPermission = (ok = true, reason = null) => ({ ok, reason })

/**
 * One receipt step of the handoff's history.
 * @param {Pick<Schemas['HandoffHistoryRow'], 'id' | 'fromStatus' | 'toStatus'> & Partial<Schemas['HandoffHistoryRow']>} r
 * @returns {Schemas['HandoffHistoryRow']}
 */
const handoffHistoryRow = (r) => ({ revisionId: null, reason: null, criteriaOutcome: null, createdAt: stamp, createdBy: null, actor: null, ...r })

/**
 * The snapshot a submission froze, with the source revision it named.
 * @param {Pick<Schemas['HandoffRevisionRow'], 'id' | 'intendedUse' | 'acceptanceCriteria'> & Partial<Omit<Schemas['HandoffRevisionRow'], 'source'>> & { source?: Partial<Schemas['HandoffRevisionSource']> }} r
 * @returns {Schemas['HandoffRevisionRow']}
 */
const handoffRevisionRow = ({ source, ...r }) => ({
  previousRevisionId: null, neededBy: '2026-09-29', promisedBy: '2026-09-29', sendingOwnerId: none, receivingOwnerId: none, targetTaskId: null, targetDeliverableId: null,
  response: null, createdAt: stamp, createdBy: null,
  source: { id: none, sourceKey: 'P-DEMO-D001', title: 'Survey corridor', revision: 'A', url: 'https://example.test/survey-A.pdf', sourceRowVersion: 0, createdAt: stamp, createdBy: null, ...source },
  ...r,
})

/**
 * One handoff as the panel reads it.
 * @param {Pick<Schemas['HandoffDetail'], 'row'> & Partial<Schemas['HandoffDetail']>} r
 * @returns {Schemas['HandoffDetail']}
 */
const handoffDetail = (r) => ({
  permissions: { edit: handoffPermission(false), assign: handoffPermission(false), transitions: [] }, changeAssessments: [], history: [], revisions: [], ...r,
})

/**
 * The answer to a create, a draft save, a transition or a reassignment.
 * @param {string} id
 * @param {number} rowVersion
 * @returns {Schemas['CommandResult']}
 */
const handoffCommand = (id, rowVersion) => ({ id, rowVersion })

module.exports = { handoffRow, handoffPage, handoffOptions, handoffSource, handoffTask, handoffPermission, handoffHistoryRow, handoffRevisionRow, handoffDetail, handoffCommand }
