// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */

/**
 * One register row typed against the contract, so a server rename of a field fails `npm run typecheck:tests`.
 * Only what a test cares about is passed in; the rest is an unassigned Civil deliverable with no dates.
 * @param {Partial<Schemas['DeliverableRow']> & Pick<Schemas['DeliverableRow'], 'id' | 'projectId' | 'key' | 'name' | 'projectDisciplineId'>} r
 * @returns {Schemas['DeliverableRow']}
 */
const deliverableRow = (r) => ({
  deliverableTypeId: '00000000-0000-0000-0000-000000000030', ownerId: null, reviewerId: null, milestoneId: null, startDate: null, dueDate: null, originalDueDate: null, originalStartDate: null,
  priority: 'Medium', status: 'Not Started', revision: null, issuedDate: null, issuedTo: null, requiresReview: false, requiredReviewPackageId: null, rowVersion: 1,
  createdAt: '2026-10-01T00:00:00Z', lastActivityAt: '2026-10-01T00:00:00Z', disciplineName: 'Civil', disciplineColour: '#467762', disciplineOrder: 1, typeName: 'Drawing Package',
  ownerName: null, ownerActive: true, reviewerName: null, milestoneKey: null, milestoneName: null, milestoneDate: null, state: null, tasks: null, ...r,
})

/**
 * A deliverable's derived state with nothing flagged and no tasks.
 * @param {Partial<Schemas['DeliverableStateRead']>} [r]
 * @returns {Schemas['DeliverableStateRead']}
 */
const deliverableState = (r = {}) => ({
  progressPct: 0, taskTotal: 0, taskComplete: 0, taskCancelled: 0, taskOpen: 0, taskOverdue: 0, taskBlocked: 0, estimatedHoursTotal: 0, remainingHours: 0, isOverdue: false, daysOverdue: 0,
  isDueSoon: false, isAtRisk: false, atRiskReasons: [], isUnassigned: false, isStale: false, isDateInconsistent: false, inconsistencyDetail: [], slipDays: 0, isInactiveOwner: false,
  issuedWithOpenWork: false, milestoneCancelled: false, isWaiting: false, isBlocked: false, blockedBy: [], blockingCount: 0, derivedPredecessorIds: [], derivedSuccessorIds: [], ...r,
})

/**
 * The register's answer for one project.
 * @param {Schemas['DeliverableRow'][]} [rows]
 * @returns {Schemas['DeliverableRow'][]}
 */
const deliverableList = (rows = []) => rows

/**
 * A deliverable's panel answer: read-only, with nothing linked, issued or derived.
 * @param {Pick<Schemas['DeliverableDetail'], 'deliverable' | 'project'> & Partial<Omit<Schemas['DeliverableDetail'], 'permissions'>> & { permissions?: Partial<Schemas['DeliverablePermissions']> }} r
 * @returns {Schemas['DeliverableDetail']}
 */
const deliverableDetail = ({ permissions, ...r }) => ({
  description: null, transmittalUrl: null, issueNote: null, acceptedDate: null, onHoldReason: null, cancelledReason: null, previousStatus: null,
  derivedPredecessors: [], derivedSuccessors: [], issues: [], explicitPredecessors: [], explicitSuccessors: [],
  permissions: { edit: { ok: false, reason: null }, delete: false, transitions: [], issue: false, linkDeliverables: false, issueNeedsConfirm: false, readyToIssueGuard: null,
    comment: false, createTask: false, needsReason: false, ...permissions },
  ...r,
})

/**
 * What creating a deliverable answers: the new id, its key, row version and due date, and any warnings.
 * @param {Pick<Schemas['DeliverableCreated'], 'id' | 'key'> & Partial<Schemas['DeliverableCreated']>} r
 * @returns {Schemas['DeliverableCreated']}
 */
const deliverableCreated = (r) => ({ rowVersion: 0, dueDate: null, warnings: [], ...r })

module.exports = { deliverableRow, deliverableState, deliverableList, deliverableDetail, deliverableCreated }
