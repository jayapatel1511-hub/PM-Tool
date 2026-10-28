// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */
const stamp = '2026-10-03T12:00:00Z'

/**
 * One weekly promise as the Readiness list and the promise detail carry it, typed against the contract so a server rename of a
 * field fails `npm run typecheck:tests`. The stored row's audit fields are in because the response carries them.
 * @param {string} projectId
 * @param {Pick<Schemas['CommitmentRow'], 'id' | 'key' | 'targetId' | 'performerId' | 'weekStart' | 'targetDate' | 'intendedOutput' | 'completionCriteria' | 'state' | 'rowVersion'> & Partial<Schemas['CommitmentRow']>} c
 * @returns {Schemas['CommitmentRow']}
 */
const commitmentRow = (projectId, c) => ({
  seq: 1, snapshotId: null, targetType: 'Task', readinessAtCommit: 'Needs Assessment', completionEvidenceUrl: null,
  auditType: 'OutputCommitment', auditKey: c.key, auditName: c.intendedOutput, projectId, auditProjectId: projectId,
  createdAt: stamp, createdBy: null, updatedAt: stamp, updatedBy: null, ...c,
})

/**
 * The recorded-week read. Paging fields are in because the Readiness page always asks for a page.
 * @param {Schemas['CommitmentRow'][]} commitments
 * @param {{ total?: number, page?: number, pageSize?: number }} [paging]
 * @returns {Schemas['CommitmentList']}
 */
const commitmentList = (commitments, paging = {}) => ({
  commitments, total: paging.total ?? commitments.length, page: paging.page ?? 1, pageSize: paging.pageSize ?? 50, truncated: false, snapshots: [],
})

/**
 * One promise with its history; no action is open unless a flag says so.
 * @param {Schemas['CommitmentRow']} commitment
 * @param {{ id: string, fromState: string, toState: string, reason: string, actorId: string }[]} events
 * @returns {Schemas['CommitmentDetail']}
 */
const commitmentDetail = (commitment, events) => ({
  commitment,
  events: events.map(e => ({ commitmentId: commitment.id, evidenceUrl: null, auditType: 'OutputCommitmentEvent', projectId: commitment.projectId, auditProjectId: commitment.projectId,
    auditKey: null, auditName: null, createdAt: stamp, createdBy: null, updatedAt: stamp, updatedBy: null, rowVersion: 0, ...e })),
  canCommit: false, canRecordMet: false, canRecordNotMet: false, canWithdraw: false,
})
module.exports = { commitmentRow, commitmentList, commitmentDetail }
