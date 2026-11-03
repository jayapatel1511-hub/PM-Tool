// @ts-check
/** Synthetic identities only. No real staff, HR records or credentials. */
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */
const { meView, referenceData, referenceOffice, referenceDiscipline } = require('./me.fixture.cjs')
const { projectDetail, projectDiscipline, projectRow, projectList } = require('./projects.fixture.cjs')
const { teamMember, teamDiscipline, projectTeam } = require('./team.fixture.cjs')
const { planningPerson, planningCell, planningGrid } = require('./planning.fixture.cjs')
const { availabilityPerson, availabilityDay, availabilityRead, availabilityConcern, hrIdentity, importRow, leaveImportPreview } = require('./availability.fixture.cjs')
const { adminUser } = require('./admin.fixture.cjs')

/** @param {number} n */
const id = n => '00000000-0000-0000-0000-' + String(n).padStart(12, '0')
const ids = { viewer: id(8100), person: id(8101), other: id(8102), supervisor: id(8103), otherSupervisor: id(8104), unplaced: id(8105), unit: id(8110), office: id(8111), discipline: id(8112), level: id(8113), project: id(8120), preview: id(8130) }
const week = '2026-10-26'
const people = [
  { id: ids.person, displayName: 'Synthetic Morgan Member', supervisorId: ids.supervisor },
  { id: ids.other, displayName: 'Synthetic Robin Member', supervisorId: ids.otherSupervisor },
  { id: ids.supervisor, displayName: 'Synthetic Same Supervisor', supervisorId: null },
  { id: ids.otherSupervisor, displayName: 'Synthetic Same Supervisor', supervisorId: null },
  { id: ids.unplaced, displayName: 'Synthetic Unplaced Person', supervisorId: null },
]
/** @param {string} personId @returns {Schemas['Organisation']} */
function facts(personId) {
  const person = people.find(p => p.id === personId)
  const supervisor = people.find(p => p.id === person?.supervisorId)
  return { supervisor: supervisor ? { id: supervisor.id, name: supervisor.displayName } : null,
    office: { id: ids.office, name: 'Synthetic office' }, homeDiscipline: { id: ids.discipline, name: 'Synthetic discipline' },
    unit: personId === ids.unplaced ? null : { id: ids.unit, name: 'Synthetic organisation unit' } }
}
const actor = meView({ id: ids.viewer, displayName: 'Synthetic Viewer', email: 'viewer@example.test', systemRoles: ['StandardUser'],
  capabilities: { workload: true, admin: true, portfolio: true, readOnly: false },
  settings: { today: '2026-10-31', dateFormat: 'yyyy-MM-dd', allowSelfReview: true }, flags: { people_screens: true, visual_system: true, capacity_screens: true, org_structure: true,
    capacity_polish: false, home_overview: false, work_screens: false, project_screens: false, live_events: false, onboarding: false, data_entry_safety: true } })
const sourceProject = projectDetail({ id: ids.project, projectNumber: 'P-SYN-ORG', name: 'Synthetic organisation context project', projectManagerId: ids.person,
  pmName: people[0].displayName, disciplines: [projectDiscipline({ id: ids.discipline, disciplineId: ids.discipline, name: 'Synthetic discipline' })] })
const projects = projectList([projectRow({ id: ids.project, projectNumber: sourceProject.projectNumber, name: sourceProject.name, pm: { id: ids.person, displayName: people[0].displayName } })])
const reference = referenceData({ offices: [referenceOffice(ids.office, 'Synthetic office')], disciplines: [referenceDiscipline(ids.discipline, 'Synthetic discipline')] })
const members = people.slice(0, 2).map((p, i) => teamMember({ id: id(8140 + i), userId: p.id, displayName: p.displayName, organisationFacts: facts(p.id), primaryDisciplineId: ids.discipline }))
const team = projectTeam({ canManage: false, members, disciplines: [teamDiscipline({ id: ids.discipline, name: 'Synthetic discipline', leadUserId: ids.person })] })
/** @type {Schemas['TeamRead']} */
const workspace = { projects: 1, people: members.map(p => ({ userId: p.userId, displayName: p.displayName, email: p.email, jobTitle: p.jobTitle,
  organisationFacts: p.organisationFacts, projects: [{ projectId: ids.project, projectNumber: sourceProject.projectNumber, projectName: sourceProject.name, roles: ['TeamMember'], leads: [] }] })) }
/** @type {Schemas['OrganisationResponse']} */
const organisation = { includeIndirectReports: true, levels: [{ id: ids.level, name: 'Synthetic level', sortOrder: 0 }],
  units: [{ id: ids.unit, name: 'Synthetic organisation unit', parentId: null, levelId: ids.level, level: 'Synthetic level', leaderId: ids.person,
    leader: people[0].displayName, officeId: ids.office, disciplineId: ids.discipline, closedOn: null, headCount: 4 }],
  people: people.map(p => ({ ...p, email: p.id + '@example.test', unitId: p.id === ids.unplaced ? null : ids.unit, rowVersion: 1 })),
  managerCycles: [{ id: ids.other, displayName: people[1].displayName }] }
const grid = planningGrid({ weeks: [week], currentWeek: week, people: people.slice(0, 2).map(p => planningPerson({ ...p,
  supervisorName: people.find(s => s.id === p.supervisorId)?.displayName, cells: [planningCell({ week })] })) })
/** @type {Schemas['WorkloadGrid']} */
const workload = { weeks: [week], currentWeek: week, today: '2026-10-31', defaultCapacity: 40, thresholds: { overPct: 110, amberPct: 90, underPct: 40 }, unfilledRoles: [],
  people: people.slice(0, 2).map(p => ({ ...p, supervisorName: people.find(s => s.id === p.supervisorId)?.displayName ?? null, officeId: ids.office, homeDisciplineId: ids.discipline,
    jobTitle: 'Synthetic designer', capacity: 40, capacityOverride: false, canSetCapacity: false, cluster: false, noDueDate: 0, openTasks: 0, overAssigned: false, overWeek: null,
    overdue: 0, partialScope: false, projects: 1, indicator: 'ok', underAssigned: false, unestimated: 0,
    cells: [{ week, hours: 8, pct: 20, available: 40, confirmed: 8, proposed: 0, committed: 8, band: 'light', uncommitted: 32 }] })) }
const availability = availabilityRead({ people: people.map((p, i) => availabilityPerson({ id: p.id, displayName: p.displayName, canManage: i === 0, days: [availabilityDay({ date: '2026-10-27' })] })),
  coverage: ['Assignee', 'Reviewer'].map(role => availabilityConcern({ id: id(8150), personId: ids.person, projectId: ids.project, role })) })
const identities = people.map(p => hrIdentity({ id: p.id, displayName: p.displayName, hrPersonKey: p.id }))
const preview = leaveImportPreview({ previewId: ids.preview, creates: 1,
  rows: [importRow({ externalId: 'synthetic-preview-1', personId: ids.person, fromDate: '2026-10-27', throughDate: '2026-10-28' })],
  affectedWeeks: [{ personId: ids.person, fromWeek: week, throughWeek: week }] })
const adminUsers = people.map(p => adminUser({ ...p, email: p.id + '@example.test' }))
/** @param {string} personId @param {number} [page] @param {number} [pageSize] @returns {Schemas['PersonPanelRead']} */
function personRead(personId, page = 1, pageSize = 25) {
  const person = people.find(p => p.id === personId)
  if (!person) throw new Error('Fixture person is not a known User')
  const memberships = Array.from({ length: 60 }, (_, n) => ({ memberId: id(8200 + n), projectId: n ? id(8300 + n) : ids.project,
    projectNumber: n ? 'P-SYN-' + n : sourceProject.projectNumber, projectName: n ? 'Synthetic permitted project ' + n : sourceProject.name,
    projectStatus: 'Active', primaryDiscipline: { id: ids.discipline, name: 'Synthetic discipline' }, roles: ['TeamMember'], leadOf: [], responsibilityNote: null }))
  return { id: person.id, displayName: person.displayName, jobTitle: 'Synthetic designer', isActive: true, organisationFacts: facts(personId),
    today: '2026-10-31', week, asOf: '2026-11-01T00:00:00Z', projects: { page, pageSize, total: memberships.length, items: memberships.slice((page - 1) * pageSize, page * pageSize) } }
}
module.exports = { ids, week, people, facts, actor, sourceProject, projects, reference, members, team, workspace, organisation, grid, workload, availability, identities, preview, adminUsers, personRead }
