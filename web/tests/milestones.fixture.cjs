// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */

/**
 * One milestone as the register and the panel read it, typed against the contract so a server rename of a field fails `npm run typecheck:tests`.
 * Only what a test cares about is passed in; the rest is an open Design Submission on 20 October with nothing aimed at it. The baseline fields are
 * absent, as they are while `schedule_baseline` is off.
 * @param {Pick<Schemas['MilestoneMilestoneRow'], 'id' | 'projectId' | 'key' | 'name'> & Partial<Schemas['MilestoneMilestoneRow']>} r
 * @returns {Schemas['MilestoneMilestoneRow']}
 */
const milestoneRow = (r) => ({
  milestoneType: 'Design Submission', date: '2026-10-20', originalDate: '2026-10-20', description: null, projectDisciplineId: null, completesPhaseId: null, isClientFacing: true,
  isComplete: false, completedDate: null, isCancelled: false, cancelledReason: null, rowVersion: 1, isSubmission: false, status: 'Open', statusReasons: [], slipDays: 0, daysRemaining: null,
  deliverableTotal: 0, deliverableIssued: 0, taskTotal: 0, taskComplete: 0, taskOpen: 0, taskOverdue: 0, taskBlocked: 0, ...r,
})

/**
 * The milestone panel's read: the caller may not manage it (the reason is null unless given), may comment, and nothing is aimed at it or completed by it.
 * @param {Pick<Schemas['MilestoneDetail'], 'milestone'> & { project?: Partial<Schemas['MilestoneProject']>, permissions?: Partial<Schemas['MilestonePermissions']> } & Partial<Omit<Schemas['MilestoneDetail'], 'milestone' | 'project' | 'permissions'>>} r
 * @returns {Schemas['MilestoneDetail']}
 */
const milestoneDetail = ({ project, permissions, ...r }) => ({
  deliverables: [], directTasks: [], completesPhase: null,
  project: { id: r.milestone.projectId, projectNumber: 'P-MOCK', name: 'Synthetic live project', status: 'Active', ...project },
  permissions: { manage: { ok: false, reason: null }, comment: true, ...permissions },
  ...r,
})

/**
 * What a dry run of a date change answers: the milestone's move and the rows it would shift, none unless given.
 * @param {Schemas['DateMove'] & Partial<Omit<Schemas['DatePreview'], 'milestone'>>} r
 * @returns {Schemas['DatePreview']}
 */
const datePreview = ({ deliverables = [], tasks = [], inconsistent = [], ...milestone }) => ({ milestone, deliverables, tasks, inconsistent })

/**
 * The answers of the milestone commands: the new milestone, an edit, a completion, a cancellation and a real date move, each with the row version the server wrote.
 * @param {string} id
 * @param {string} key
 * @returns {Schemas['MilestoneCreated']}
 */
const milestoneCreated = (id, key) => ({ id, key, rowVersion: 0 })
/** @param {string} id @param {number} rowVersion @returns {Schemas['MilestoneVersion']} */
const milestoneVersion = (id, rowVersion) => ({ id, rowVersion })
/** @param {string} id @param {number} rowVersion @param {Schemas['MilestonePhase'] | null} [suggestPhase] @returns {Schemas['MilestoneCompleted']} */
const milestoneCompleted = (id, rowVersion, suggestPhase = null) => ({ id, rowVersion, suggestPhase })
/** @param {string} id @param {number} rowVersion @param {Schemas['RetargetDeliverable'][]} [retarget] @returns {Schemas['MilestoneCancelled']} */
const milestoneCancelled = (id, rowVersion, retarget = []) => ({ id, rowVersion, retarget })
/** @param {string} id @param {number} rowVersion @param {Schemas['DatePreview']} preview @returns {Schemas['DateChanged']} */
const dateChanged = (id, rowVersion, preview) => ({ id, rowVersion, shifted: preview.deliverables.length + preview.tasks.length, preview })

module.exports = { milestoneRow, milestoneDetail, datePreview, milestoneCreated, milestoneVersion, milestoneCompleted, milestoneCancelled, dateChanged }
