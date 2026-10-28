// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */

const none = '00000000-0000-4000-8000-000000000000'
const stamp = '2026-09-27T09:00:00Z'
const audit = { createdAt: stamp, createdBy: null, updatedAt: stamp, updatedBy: null }

/**
 * A registered source revision, typed against the contract so a server rename of a field fails `npm run typecheck:tests`.
 * @param {Pick<Schemas['SourceRevisionRow'], 'id' | 'sourceKey' | 'title' | 'revision'> & Partial<Schemas['SourceRevisionRow']>} r
 * @returns {Schemas['SourceRevisionRow']}
 */
const sourceRevisionRow = (r) => ({
  projectId: none, deliverableId: null, sourceRowVersion: 0, sourceIdentity: 'survey', sourceSystem: 'Manual', externalIdentifier: 'survey', issuer: 'Survey team', scope: 'East corridor',
  sourceCheckedAt: null, supersedesId: null, authorIds: [], identityHash: 'hash', url: 'https://example.test/A.pdf', auditType: 'SourceRevision', auditProjectId: null, auditKey: null, auditName: null,
  ...audit, rowVersion: 0, ...r,
})

/**
 * The current revision of one source identity.
 * @param {Pick<Schemas['SourceHeadRow'], 'id' | 'identity' | 'currentRevisionId'> & Partial<Schemas['SourceHeadRow']>} r
 * @returns {Schemas['SourceHeadRow']}
 */
const sourceHeadRow = (r) => ({
  ownerId: none, projectDisciplineId: none, auditType: 'SourceHead', projectId: none, auditProjectId: null, auditKey: null, auditName: null, ...audit, rowVersion: 0, ...r,
})

/**
 * @param {Pick<Schemas['CoordinationSource'], 'revision'> & Partial<Schemas['CoordinationSource']>} r
 * @returns {Schemas['CoordinationSource']}
 */
const coordinationSource = (r) => ({ identity: r.revision.sourceIdentity, published: true, isCurrent: true, ...r })

/**
 * @param {Pick<Schemas['CoordinationDeliverable'], 'id' | 'key' | 'name' | 'projectDisciplineId'> & Partial<Schemas['CoordinationDeliverable']>} r
 * @returns {Schemas['CoordinationDeliverable']}
 */
const coordinationDeliverable = (r) => ({ ownerId: null, rowVersion: 0, revision: null, transmittalUrl: null, status: 'In Progress', canCommit: false, canManage: false, ...r })

/**
 * @param {Pick<Schemas['CoordinationTask'], 'id' | 'key' | 'name' | 'projectDisciplineId'> & Partial<Schemas['CoordinationTask']>} r
 * @returns {Schemas['CoordinationTask']}
 */
const coordinationTask = (r) => ({ ownerId: null, rowVersion: 0, status: 'In Progress', canCommit: false, canManage: false, ...r })

/**
 * What the Changes and Reviews registers choose from, answered by both options routes. The fields given replace the defaults.
 * @param {Partial<Schemas['CoordinationOptions']>} [r]
 * @returns {Schemas['CoordinationOptions']}
 */
const coordinationOptions = (r = {}) => ({
  actorId: none, canWrite: true, manageDisciplineIds: [], canCreate: { changes: false, reviews: false }, people: [], disciplines: [], sources: [], heads: [], deliverables: [], tasks: [], ...r,
})

/**
 * One package of the Reviews register.
 * @param {Pick<Schemas['ReviewRow'], 'id' | 'key' | 'title'> & Partial<Schemas['ReviewRow']>} r
 * @returns {Schemas['ReviewRow']}
 */
const reviewRow = (r) => ({ status: 'Draft', coordinatorId: none, roundNumber: 1, rowVersion: 1, outstandingDisciplines: 0, blockingFindings: 0, waitingDays: 0, ...r })

/**
 * One page of the Reviews register. The page and size are the request's, as the server echoes them.
 * @param {Schemas['ReviewRow'][]} items
 * @param {{ total?: number, page?: number, pageSize?: number }} [paging]
 * @returns {Schemas['PageOfReviewRow']}
 */
const reviewPage = (items, paging = {}) => ({ items, page: paging.page ?? 1, pageSize: paging.pageSize ?? 50, totalCount: paging.total ?? items.length })

/**
 * @param {Pick<Schemas['ReviewPackageRow'], 'id' | 'key' | 'title'> & Partial<Schemas['ReviewPackageRow']>} r
 * @returns {Schemas['ReviewPackageRow']}
 */
const reviewPackageRow = (r) => ({
  purpose: 'Check clearance and the shared corridor', coordinatorId: none, projectDisciplineId: none, status: 'Draft', currentRoundId: null, roundNumber: 1, requiredForIssue: true,
  auditType: 'ReviewPackage', auditProjectId: null, auditKey: null, auditName: null, auditDisciplineId: null, projectId: none, seq: 1, deletedAt: null, deletedBy: null, ...audit, rowVersion: 1, ...r,
})

/**
 * @param {Pick<Schemas['ReviewRoundRow'], 'id' | 'number'> & Partial<Schemas['ReviewRoundRow']>} r
 * @returns {Schemas['ReviewRoundRow']}
 */
const reviewRoundRow = (r) => ({
  packageId: none, purpose: 'Check clearance and the shared corridor', reason: null, removalImpact: null, startedAt: null, status: 'Draft', auditType: 'ReviewRound', projectId: none,
  auditProjectId: null, auditKey: null, auditName: null, ...audit, rowVersion: 1, ...r,
})

/**
 * One discipline's review of a round.
 * @param {Pick<Schemas['ReviewAssignmentRow'], 'id' | 'roundId' | 'projectDisciplineId' | 'reviewerId'> & Partial<Schemas['ReviewAssignmentRow']>} r
 * @returns {Schemas['ReviewAssignmentRow']}
 */
const reviewAssignmentRow = (r) => ({
  dueDate: '2026-09-18', status: 'Pending', rationale: null, decidedAt: null, decidedBy: null, auditType: 'DisciplineReview', projectId: none, auditProjectId: null, auditKey: null, auditName: null,
  ...audit, rowVersion: 1, ...r,
})

/**
 * @param {Pick<Schemas['ReviewFindingRow'], 'id' | 'roundId' | 'sourceRevisionId' | 'projectDisciplineId' | 'resolverId' | 'verifierId' | 'text'> & Partial<Schemas['ReviewFindingRow']>} r
 * @returns {Schemas['ReviewFindingRow']}
 */
const reviewFindingRow = (r) => ({
  packageId: none, carriedFromId: null, issueId: null, originatorId: none, severity: 'Blocking', status: 'Open', response: null, evidenceUrl: null, withdrawalAcknowledgedBy: null,
  auditType: 'ReviewFinding', auditName: null, projectId: none, auditProjectId: null, auditKey: null, ...audit, rowVersion: 1, ...r,
})

/**
 * @param {Pick<Schemas['ReviewFindingEventRow'], 'id' | 'findingId' | 'action'> & Partial<Schemas['ReviewFindingEventRow']>} r
 * @returns {Schemas['ReviewFindingEventRow']}
 */
const reviewFindingEventRow = (r) => ({
  reason: 'Checked and recorded supporting evidence', evidenceUrl: null, auditType: 'FindingEvent', projectId: none, auditProjectId: null, auditKey: null, auditName: null, ...audit, rowVersion: 0, ...r,
})

/**
 * A source revision a round reviews.
 * @param {Pick<Schemas['ReviewManifestRow'], 'id' | 'roundId' | 'sourceRevisionId' | 'source'> & Partial<Schemas['ReviewManifestRow']>} r
 * @returns {Schemas['ReviewManifestRow']}
 */
const reviewManifestRow = (r) => ({ deliverableId: none, authorIds: [], ...r })

/**
 * One package as the detail panel reads it; the viewer may do nothing unless the can flags say so.
 * @param {Pick<Schemas['ReviewDetail'], 'package'> & Partial<Schemas['ReviewDetail']>} r
 * @returns {Schemas['ReviewDetail']}
 */
const reviewDetail = (r) => ({
  rounds: [], assignments: [], findings: [], manifest: [], events: [], eligiblePeople: [], actorId: none, canCoordinate: false, canManage: false, canWrite: false, canReassignVerifier: false, ...r,
})

module.exports = {
  sourceRevisionRow, sourceHeadRow, coordinationSource, coordinationDeliverable, coordinationTask, coordinationOptions,
  reviewRow, reviewPage, reviewPackageRow, reviewRoundRow, reviewAssignmentRow, reviewFindingRow, reviewFindingEventRow, reviewManifestRow, reviewDetail,
}
