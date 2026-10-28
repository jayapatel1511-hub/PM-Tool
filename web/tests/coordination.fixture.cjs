// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */

const none = '00000000-0000-4000-8000-000000000000'

/**
 * One input-use row of the project coordination projection, typed against the contract so a server rename of a field fails `npm run typecheck:tests`.
 * @param {Pick<Schemas['CoordinationInputUseRow'], 'id' | 'targetId' | 'sourceRevisionId' | 'intendedUse'> & Partial<Schemas['CoordinationInputUseRow']>} r
 * @returns {Schemas['CoordinationInputUseRow']}
 */
const coordinationUseRow = (r) => ({
  targetType: 'Task', ownerId: none, sourceIdentity: 'survey', adoptedAt: '2026-10-01T12:00:00Z', adoptedBy: none, rowVersion: 1,
  targetKey: null, targetName: null, sourceUrl: null, sourceKey: null, revision: null, ...r,
})

/**
 * One change-notice row, with the scoped counts beside the whole-project count.
 * @param {Pick<Schemas['CoordinationChangeRow'], 'id' | 'key' | 'title' | 'ownerId'> & Partial<Schemas['CoordinationChangeRow']>} r
 * @returns {Schemas['CoordinationChangeRow']}
 */
const coordinationChangeRow = (r) => ({ status: 'Open', pendingAssessments: 1, acknowledgedPending: 0, projectPending: 1, ...r })

/**
 * @param {Pick<Schemas['CoordinationReviewRow'], 'id' | 'key' | 'title' | 'coordinatorId'> & Partial<Schemas['CoordinationReviewRow']>} r
 * @returns {Schemas['CoordinationReviewRow']}
 */
const coordinationReviewRow = (r) => ({ status: 'In Review', outstandingDisciplines: 1, blockingFindings: 0, ...r })

/**
 * @param {Pick<Schemas['CoordinationIssueRow'], 'id' | 'key' | 'title' | 'ownerId'> & Partial<Schemas['CoordinationIssueRow']>} r
 * @returns {Schemas['CoordinationIssueRow']}
 */
const coordinationIssueRow = (r) => ({ status: 'Open', ownerName: null, projectDisciplineId: null, ...r })

/**
 * @param {Pick<Schemas['SubmissionContextRow'], 'id' | 'key' | 'title' | 'coordinatorId' | 'targetDate'> & Partial<Schemas['SubmissionContextRow']>} r
 * @returns {Schemas['SubmissionContextRow']}
 */
const coordinationSubmissionRow = (r) => ({ status: 'Checking', effectiveStatus: 'Checking', failingChecks: [], ...r })

/**
 * @param {Pick<Schemas['StaffingContextRow'], 'id' | 'key' | 'targetId' | 'description' | 'neededBy' | 'affectedOwnerId' | 'removalOwnerId'> & Partial<Schemas['StaffingContextRow']>} r
 * @returns {Schemas['StaffingContextRow']}
 */
const coordinationStaffingRow = (r) => ({ targetType: 'Task', state: 'Open', ...r })

/**
 * The project coordination projection with every block empty and unpaged; the fields given replace the defaults.
 * @param {Partial<Schemas['CoordinationProjection']>} [r]
 * @returns {Schemas['CoordinationProjection']}
 */
const coordinationProjection = (r = {}) => ({
  evaluatedAt: '2026-10-01T12:00:00Z',
  handoffs: [], outgoing: [], incoming: [], outgoingPage: 1, incomingPage: 1, outgoingTotal: 0, incomingTotal: 0, handoffPage: 1, handoffPageSize: 0, handoffsTotal: 0,
  changeTargets: [], unavailableChangeTargets: [], changeTargetsTotal: 0, unavailableChangeTargetsTotal: 0,
  reviews: [], reviewsPage: 1, reviewsPageSize: 0, reviewsTotal: 0,
  uses: [], usesTotal: 0, usesPage: 1, usesPageSize: 0,
  linkedIssues: [], linkedIssuesTotal: 0, linkedIssuesPage: 1, linkedIssuesPageSize: 0,
  changes: [], changesTotal: 0, changesPage: 1, changesPageSize: 0,
  blockerGroups: [], blockerGroupsTotal: 0, blockerGroupsPage: 1,
  startability: [], startabilityTotal: 0, startabilityPage: 1, startabilityPageSize: 0, startabilityFrom: '2026-10-01', startabilityTo: '2026-10-31', startabilityReadyTotal: 0,
  linkedActions: [], linkedActionsTotal: 0,
  upcomingSubmissions: [], upcomingSubmissionsTotal: 0, upcomingSubmissionsPage: 1,
  staffingConflicts: [], staffingConflictsTotal: 0, staffingConflictsPage: 1,
  page: 1, pageSize: 0, ...r,
})
module.exports = { coordinationUseRow, coordinationChangeRow, coordinationReviewRow, coordinationIssueRow, coordinationSubmissionRow, coordinationStaffingRow, coordinationProjection }
