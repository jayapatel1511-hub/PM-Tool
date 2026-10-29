// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */

/**
 * The derived state of a task with nothing wrong beyond having no owner or due date: no blocker, nothing overdue, every list empty.
 * @param {Partial<Schemas['State']>} [r]
 * @returns {Schemas['State']}
 */
const taskState = (r = {}) => ({
  isOverdue: false, daysOverdue: 0, isDueSoon: false, isWaiting: false, isBlocked: false, blockedSince: null, daysBlocked: 0, blockedBy: [], isBlocking: false, blockingCount: 0,
  blockingTaskIds: [], isStale: false, staleDays: 0, isUnassigned: true, isMissingDueDate: true, isDateInconsistent: false, inconsistencyDetail: [], isInactiveOwner: false,
  isHeldPastDue: false, isReviewStalled: false, affectedMilestoneIds: [], notes: [], evaluatedAt: '2026-10-01T00:00:00Z', ...r,
})

/**
 * One task as the lists, the board and the panel read it: Not Started in Civil, medium priority, with no owner, reviewer, dates, deliverable or milestone unless given,
 * and the caller allowed to assign, edit, change the due date and block it.
 * @param {Pick<Schemas['TaskQueriesRow'], 'id' | 'projectId' | 'key' | 'name'> & Partial<Schemas['TaskQueriesRow']>} r
 * @returns {Schemas['TaskQueriesRow']}
 */
const taskRow = (r) => ({
  projectNumber: 'P-MOCK', projectName: 'Synthetic live project', projectStatus: 'Active', projectDisciplineId: '00000000-0000-0000-0000-000000000011', disciplineName: 'Civil', disciplineCode: 'CIV',
  disciplineColour: null, disciplineOrder: 0, deliverableId: null, deliverableKey: null, deliverableName: null, deliverableDueDate: null, deliverableStatus: null, deliverableOrder: null,
  milestoneId: null, effectiveMilestoneId: null, milestoneKey: null, milestoneName: null, milestoneDate: null, milestoneDerived: false, assigneeId: null, assigneeName: null,
  assigneeActive: true, reviewerId: null, reviewerName: null, requiresReview: false, priority: 'Medium', startDate: null, dueDate: null, originalStartDate: null, originalDueDate: null,
  status: 'Not Started', lane: 'To Do', progressPct: 0, estimatedHours: null, reviewRound: 0, dueDateChangeCount: 0, lastActivityAt: '2026-10-01T00:00:00Z', statusChangedAt: null,
  completedAt: null, createdAt: '2026-10-01T00:00:00Z', createdBy: null, createdByName: null, sortOrder: 1, rowVersion: 1, manualBlockType: null, manualBlockReason: null,
  manualBlockSetAt: null, commentCount: 0, predecessorCount: 0, successorCount: 0, collaboratorIds: [], can: { assign: true, edit: true, changeDue: true, block: true }, state: taskState(), ...r,
})

/**
 * One page of a task list: every row on one page, with the total the rows make unless given.
 * @param {Schemas['TaskQueriesRow'][]} items
 * @param {Partial<Schemas['PageOfRow']>} [r]
 * @returns {Schemas['PageOfRow']}
 */
const taskPage = (items, r = {}) => ({ items, page: 1, pageSize: 200, totalCount: items.length, sort: null, ...r })

/**
 * What one status change allows, as the panel's menu reads it.
 * @param {string} to
 * @param {Partial<Schemas['TaskDetailTransition']>} [r]
 * @returns {Schemas['TaskDetailTransition']}
 */
const taskTransition = (to, r = {}) => ({ to, allowed: true, reason: null, needsReason: false, via: null, ...r })

/**
 * A permission the panel reads: allowed, with no reason unless refused.
 * @param {boolean} [ok]
 * @param {string | null} [reason]
 * @returns {Schemas['TaskDetailPermission']}
 */
const taskPermission = (ok = true, reason = null) => ({ ok, reason })

/**
 * Every permission the task panel reads: the caller may do all of it, with no transition offered unless given.
 * @param {Partial<Schemas['TaskDetailPermissions']>} [r]
 * @returns {Schemas['TaskDetailPermissions']}
 */
const taskPermissions = (r = {}) => ({
  edit: taskPermission(), assign: taskPermission(), setReviewer: taskPermission(), dueNeedsReason: false, dueDate: taskPermission(), block: taskPermission(), delete: taskPermission(),
  restore: true, comment: true, transitions: [], completeHint: null, isReviewer: false, dependencies: true, enterTime: true, needsReason: false, allowSelfReview: true, authoriseStart: false, ...r,
})

/**
 * The task panel's read: the row, its project, nobody on it and no reasons unless given.
 * @param {Pick<Schemas['TaskDetailResponse'], 'task'> & Partial<Schemas['TaskDetailResponse']>} r
 * @returns {Schemas['TaskDetailResponse']}
 */
const taskDetail = (r) => ({
  description: null, onHoldReason: null, cancelledReason: null, previousStatus: null, reviewRequestedAt: null,
  project: { id: r.task.projectId, projectNumber: r.task.projectNumber, name: r.task.projectName, status: r.task.projectStatus }, collaborators: [], watchers: [], permissions: taskPermissions(), ...r,
})

/**
 * The answers of the task commands: a created task, the version an edit leaves, a status change, a block or restore, a delete, and the delete preview.
 * @param {string} id
 * @param {string} key
 * @param {string[]} [warnings]
 * @returns {Schemas['TaskCreated']}
 */
const taskCreated = (id, key, warnings = []) => ({ id, key, rowVersion: 0, warnings })
/** @param {string} id @param {number} rowVersion @returns {Schemas['TaskEdited']} */
const taskEdited = (id, rowVersion) => ({ id, rowVersion, dueDateChangeCount: 0, warnings: [] })
/** @param {string} id @param {string} status @param {number} rowVersion @returns {Schemas['TaskTransitioned']} */
const taskTransitioned = (id, status, rowVersion) => ({ id, status, rowVersion, progressPct: 0, reviewRound: 0, warnings: [] })
/** @param {string} id @param {number} rowVersion @returns {Schemas['TaskVersion']} */
const taskVersion = (id, rowVersion) => ({ id, rowVersion })
/** @param {number} removedDependencies @returns {Schemas['TaskDeleted']} */
const taskDeleted = (removedDependencies = 0) => ({ removedDependencies })
/** @param {Partial<Schemas['TaskDeletePreview']>} [r] @returns {Schemas['TaskDeletePreview']} */
const taskDeletePreview = (r = {}) => ({ dependencies: [], suggestCancel: false, canDelete: true, canCancel: true, ...r })

module.exports = {
  taskState, taskRow, taskPage, taskTransition, taskPermission, taskPermissions, taskDetail, taskCreated, taskEdited, taskTransitioned, taskVersion, taskDeleted, taskDeletePreview,
}
