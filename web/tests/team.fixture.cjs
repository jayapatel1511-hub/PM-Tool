// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */

/**
 * A discipline on the project, active with no lead and no sort position unless given; the reference name, code and colour are the given ones.
 * @param {Pick<Schemas['ProjectTeamDiscipline'], 'id' | 'name'> & Partial<Schemas['ProjectTeamDiscipline']>} r
 * @returns {Schemas['ProjectTeamDiscipline']}
 */
const teamDiscipline = (r) => ({ disciplineId: r.id, code: r.name.slice(0, 3).toUpperCase(), colour: '#2563eb', leadUserId: null, isActive: true, sortOrder: 0, rowVersion: 1, ...r })

/**
 * A current team member: a Team Member with no primary discipline, leading nothing and not staffable by the caller unless given. The person's
 * job title and office are sent as null when unknown, as the server does.
 * @param {Pick<Schemas['ProjectTeamMember'], 'id' | 'userId' | 'displayName'> & Partial<Schemas['ProjectTeamMember']>} r
 * @returns {Schemas['ProjectTeamMember']}
 */
const teamMember = (r) => ({
  email: `${r.displayName.toLowerCase().replace(/\W+/g, '.')}@example.test`, jobTitle: null, officeId: null, isActive: true, roles: ['TeamMember'], primaryDisciplineId: null,
  addedAt: '2026-10-01T09:00:00Z', addedBy: null, rowVersion: 1, leadOf: [], isPrimaryPm: false, canStaff: false, ...r,
})

/**
 * The Team tab's read: a caller who manages the team with no reason to refuse unless given.
 * @param {Partial<Schemas['ProjectTeamRead']>} [r]
 * @returns {Schemas['ProjectTeamRead']}
 */
const projectTeam = (r = {}) => ({ disciplines: [], members: [], canManage: true, manageReason: null, ...r })

module.exports = { teamDiscipline, teamMember, projectTeam }
