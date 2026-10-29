// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */

const none = '00000000-0000-4000-8000-000000000000'
const stamp = '2026-10-01T12:00:00Z'

/**
 * The audit identity, stamps and row version every readiness record carries after its own columns.
 * @param {string} auditType
 * @param {string} [projectId]
 */
const audit = (auditType, projectId = none) => ({ auditType, projectId, auditProjectId: projectId, auditKey: null, auditName: null, createdAt: stamp, createdBy: none, updatedAt: stamp, updatedBy: none, rowVersion: 0 })

/**
 * The window of open constraints and Ready outputs, typed against the contract so a server rename of a field fails `npm run typecheck:tests`.
 * The page and size are the request's, as the server echoes them.
 * @param {Partial<Schemas['ReadinessWindow']>} [r]
 * @returns {Schemas['ReadinessWindow']}
 */
const readinessWindow = (r = {}) => ({ from: '2026-10-05', to: '2026-10-11', constraints: [], constraintsTotal: 0, constraintsPage: 1, readyPage: 1, pageSize: 50,
  constraintsTruncated: false, readyOutputs: [], readyOutputsTotal: 0, readyOutputsTruncated: false, ...r })

/**
 * One open constraint of the window.
 * @param {Pick<Schemas['ReadinessWindowConstraint'], 'id' | 'targetId'> & Partial<Schemas['ReadinessWindowConstraint']>} r
 * @returns {Schemas['ReadinessWindowConstraint']}
 */
const windowConstraint = (r) => ({ seq: 1, key: 'P-DEMO-CT001', targetType: 'Task', category: 'Handoff', description: 'Synthetic constraint', removalOwnerId: none, affectedOwnerId: none,
  neededBy: '2026-10-05', sourceUrl: 'https://example.test/input', state: 'Open', resolutionEvidenceUrl: null, verifiedBy: null, verifiedAt: null, linkedType: null, linkedId: null,
  ...audit('WorkConstraint'), ...r })

/**
 * One Ready output of the window.
 * @param {Pick<Schemas['ReadyOutput'], 'id' | 'targetId' | 'key' | 'name'> & Partial<Schemas['ReadyOutput']>} r
 * @returns {Schemas['ReadyOutput']}
 */
const readyOutput = (r) => ({ targetType: 'Task', dueDate: null, ownerId: none, disciplineId: none, intendedOutput: 'Synthetic output', completionCriteria: 'Checked', state: 'Ready', ...r })

/**
 * The assessment of one work item.
 * @param {Pick<Schemas['ReadinessAssessmentRead'], 'id' | 'targetId'> & Partial<Schemas['ReadinessAssessmentRead']>} r
 * @returns {Schemas['ReadinessAssessmentRead']}
 */
const readinessAssessment = (r) => ({ targetType: 'Task', ownerId: none, intendedOutput: 'Grading layout', completionCriteria: 'Layout checked against the permit set', state: 'Needs Assessment',
  evaluatedAt: stamp, ...audit('ReadinessAssessment'), ...r })

/**
 * One canonical check of an assessment; unassessed until it is given an answer.
 * @param {Pick<Schemas['ReadinessCheckRead'], 'id' | 'assessmentId' | 'code'> & Partial<Schemas['ReadinessCheckRead']>} r
 * @returns {Schemas['ReadinessCheckRead']}
 */
const readinessCheck = (r) => ({ applies: null, satisfied: null, reason: null, evidenceUrl: null, recordedBy: null, ...audit('ReadinessCheck'), ...r })

/**
 * One approved assumption exception.
 * @param {Pick<Schemas['ReadinessExceptionRead'], 'id' | 'assessmentId' | 'basisVersionId'> & Partial<Schemas['ReadinessExceptionRead']>} r
 * @returns {Schemas['ReadinessExceptionRead']}
 */
const readinessException = (r) => ({ approvedBy: none, verifierId: none, limitedWork: 'Only preliminary grading layout', risk: 'Utility location could alter grading', expiresOn: '2026-10-15',
  ...audit('ReadinessException'), ...r })

/**
 * A decision, issue, handoff or submission package a check or constraint points at.
 * @param {Pick<Schemas['LinkedRecord'], 'type' | 'id' | 'key' | 'title'> & Partial<Schemas['LinkedRecord']>} r
 * @returns {Schemas['LinkedRecord']}
 */
const linkedRecord = (r) => ({ status: 'Pending', ...r })

/**
 * One work item's readiness: the assessment, its checks and sources, the codes still unknown or blocked, and its exceptions.
 * @param {Pick<Schemas['ReadinessDetail'], 'assessment'> & Partial<Schemas['ReadinessDetail']>} r
 * @returns {Schemas['ReadinessDetail']}
 */
const readinessDetail = (r) => ({ checks: [], sources: [], unknown: [], blocked: [], exceptions: [], ...r })

/**
 * One constraint on a work item.
 * @param {Pick<Schemas['ReadinessConstraintRow'], 'id' | 'key' | 'targetId'> & Partial<Schemas['ReadinessConstraintRow']>} r
 * @returns {Schemas['ReadinessConstraintRow']}
 */
const readinessConstraintRow = (r) => ({ rowVersion: 0, targetType: 'Task', category: 'Decision', description: 'Await the outfall decision', removalOwnerId: none, affectedOwnerId: none,
  neededBy: '2026-10-05', sourceUrl: 'https://example.test/outfall', state: 'Open', resolutionEvidenceUrl: null, verifiedBy: null, verifiedAt: null, linkedType: null, linkedId: null,
  createdAt: stamp, createdBy: none, linked: null, ...r })

/**
 * A submission package the work waits on.
 * @param {Pick<Schemas['ReadinessPackageRef'], 'id' | 'key' | 'title'> & Partial<Schemas['ReadinessPackageRef']>} r
 * @returns {Schemas['ReadinessPackageRef']}
 */
const readinessPackageRef = (r) => ({ status: 'Draft', ...r })

/**
 * One link from a work item to a prerequisite submission package, with the package that now governs it.
 * @param {Pick<Schemas['ReadinessPrerequisiteRow'], 'id' | 'packageId' | 'package'> & Partial<Schemas['ReadinessPrerequisiteRow']>} r
 * @returns {Schemas['ReadinessPrerequisiteRow']}
 */
const readinessPrerequisiteRow = (r) => ({ rowVersion: 0, reason: 'Permit set must be issued before grading starts', createdAt: stamp, createdBy: none, removedAt: null, removedBy: null,
  removalReason: null, effective: r.package, listsOutput: false, ...r })

module.exports = { readinessWindow, windowConstraint, readyOutput, readinessAssessment, readinessCheck, readinessException, linkedRecord, readinessDetail, readinessConstraintRow,
  readinessPackageRef, readinessPrerequisiteRow }
