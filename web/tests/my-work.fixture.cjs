// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */
const { attentionList } = require('./evaluation.fixture.cjs')

/**
 * The person a My Work answer is about.
 * @param {string} id
 * @param {string} displayName
 * @param {Partial<Schemas['MyWorkPerson']>} [r]
 * @returns {Schemas['MyWorkPerson']}
 */
const myWorkPerson = (id, displayName, r = {}) => ({ id, displayName, jobTitle: null, isActive: true, ...r })

/**
 * One project row of My Work, typed against the contract so a server rename of a field fails `npm run typecheck:tests`.
 * A project the person only belongs to has no lead disciplines, no follow level and no next milestone, all sent as null or empty.
 * @param {Pick<Schemas['MyWorkProject'], 'id' | 'projectNumber' | 'name'> & Partial<Schemas['MyWorkProject']>} r
 * @returns {Schemas['MyWorkProject']}
 */
const myWorkProject = (r) => ({ status: 'Active', roles: ['TeamMember'], leads: [], follow: null, computedHealth: 'Green', reportedHealth: 'Green', nextMilestone: null, ...r })

/**
 * A person's My Work answer. Every section is present and empty unless given, as the server sends it. The task, deliverable and action rows
 * belong to the routes that list them (their conversions type them), so a suite passes those rows in as it builds them.
 * @param {Partial<Omit<Schemas['MyWorkView'], 'tasks' | 'reviews' | 'deliverables' | 'actions' | 'completed'>> & {
 *   tasks?: any[], reviews?: { tasks: any[], deliverables: any[] }, deliverables?: any[], actions?: any[], completed?: any[] }} [r]
 * @returns {Schemas['MyWorkView']}
 */
const myWork = (r = {}) => ({
  person: myWorkPerson('00000000-0000-4000-8000-000000000000', 'Synthetic person'), readOnly: false, today: '2026-10-03', attention: attentionList([]),
  tasks: [], reviews: { tasks: [], deliverables: [] }, deliverables: [], people: {}, successors: [], decisions: [], actions: [], projects: [], milestones: [], completed: [], ...r,
})

/**
 * One row of My Staff. A person with no work yet has every count at zero and no last activity, as the server sends it.
 * @param {Pick<Schemas['MyStaffPerson'], 'id' | 'displayName'> & Partial<Schemas['MyStaffPerson']>} r
 * @returns {Schemas['MyStaffPerson']}
 */
const myStaffPerson = (r) => ({
  jobTitle: null, officeId: null, isActive: true, projects: 1, roles: { pm: 0, dl: 0, team: 1, reviewer: 0, viewer: 0 }, open: 0, overdue: 0, blocked: 0, blocking: 0,
  reviews: 0, reviewsStalled: 0, deliverablesDue: 0, lastActivityAt: null, ...r,
})

/**
 * The My Staff answer over the given rows. The tiles count them as the server does unless the suite sets them.
 * @param {Schemas['MyStaffPerson'][]} people
 * @param {Partial<Schemas['MyStaffList']>} [r]
 * @returns {Schemas['MyStaffList']}
 */
const myStaffList = (people, r = {}) => ({
  scope: 'direct', canSeeAll: false,
  tiles: { staff: people.length, assignments: people.reduce((n, p) => n + p.projects, 0), withOverdue: people.filter((p) => p.overdue > 0).length,
    withBlocked: people.filter((p) => p.blocked > 0).length, reviewsStalled: people.reduce((n, p) => n + p.reviewsStalled, 0) },
  people, ...r,
})

module.exports = { myWorkPerson, myWorkProject, myWork, myStaffPerson, myStaffList }
