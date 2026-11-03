// @ts-check
/** Representative synthetic UI contracts only. SearchScreenTests owns real predicates/authority/all16 mappings. */
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */
const { meView, referenceData, referenceDiscipline } = require('./me.fixture.cjs')
const legacy = require('./person-search-reassign.fixture.cjs')
/** @param {number} n */
const id = n => '00000000-0000-0053-0000-' + String(n).padStart(12, '0')
const ids = { viewer: id(1), otherViewer: id(2), alex: id(10), jill: id(11), inactive: id(12), project: id(100), otherProject: id(101), externalProject: id(102),
  discipline: id(110), otherDiscipline: id(111), externalDiscipline: id(112), task: id(200), otherTask: id(201), electricalTask: id(202), externalTask: id(203) }
/** @param {string} [viewerId] @param {boolean} [enabled] @returns {Schemas['MeView']} */
const actorFor = (viewerId = ids.viewer, enabled = true) => meView({ id: viewerId, displayName: viewerId === ids.viewer ? 'Synthetic viewer A' : 'Synthetic viewer B', email: 'viewer@example.test',
  capabilities: { workload: false }, flags: { people_screens: enabled, visual_system: true, live_events: false, onboarding: false, work_screens: false,
    capacity_screens: false, org_structure: false, home_overview: false, screen_patterns: false, project_screens: false } })
const disciplines = [referenceDiscipline(ids.discipline, 'Civil'), referenceDiscipline(ids.otherDiscipline, 'Electrical'),
  ...Array.from({ length: 198 }, (_, n) => referenceDiscipline(id(1000 + n), 'Option discipline ' + String(n).padStart(3, '0'))), referenceDiscipline(ids.externalDiscipline, 'Zoology')]
const reference = referenceData({ disciplines })
const people = [
  { id: ids.alex, displayName: 'Synthetic Alex Person', email: 'alex@example.test', jobTitle: 'Civil EIT', homeDisciplineId: ids.discipline, isActive: true, canViewWork: true },
  { id: ids.jill, displayName: 'Synthetic Jill Person', email: 'jill@example.test', jobTitle: 'Technologist', homeDisciplineId: ids.otherDiscipline, isActive: true, canViewWork: false },
  { id: ids.inactive, displayName: 'Synthetic Chris Person', email: 'chris@example.test', jobTitle: 'Civil EIT', homeDisciplineId: ids.externalDiscipline, isActive: false, canViewWork: false },
  ...[ids.viewer, ids.otherViewer].map(viewerId => ({ id: viewerId, displayName: actorFor(viewerId).displayName, email: viewerId + '@example.test', jobTitle: null,
    homeDisciplineId: null, isActive: true, canViewWork: true })),
]
const projects = [{ id: ids.project, projectNumber: 'P-C000', name: 'Synthetic valve civil project', disciplineId: ids.discipline },
  { id: ids.otherProject, projectNumber: 'P-C001', name: 'Synthetic valve electrical project', disciplineId: ids.otherDiscipline },
  ...Array.from({ length: 198 }, (_, n) => ({ id: id(2000 + n), projectNumber: 'P-O' + String(n).padStart(3, '0'), name: 'Synthetic option project ' + n, disciplineId: ids.discipline })),
  { id: ids.externalProject, projectNumber: 'P-Z999', name: 'Synthetic valve external project', disciplineId: ids.discipline }]
const tasks = [
  { id: ids.task, key: 'P-C000-T0001', name: 'Synthetic valve review', projectId: ids.project, disciplineId: ids.discipline, status: 'In Progress', userId: ids.alex },
  { id: ids.otherTask, key: 'P-C000-T0002', name: 'Synthetic valve handover', projectId: ids.project, disciplineId: ids.discipline, status: 'Not Started', userId: ids.alex },
  { id: ids.electricalTask, key: 'P-C001-T0001', name: 'Synthetic valve electrical', projectId: ids.otherProject, disciplineId: ids.otherDiscipline, status: 'In Progress', userId: ids.jill },
  { id: ids.externalTask, key: 'P-Z999-T0001', name: 'Synthetic valve external', projectId: ids.externalProject, disciplineId: ids.discipline, status: 'In Progress', userId: ids.alex },
]
const canonical = legacy.searchRead(new URLSearchParams()).screen
if (!canonical) throw new Error('The fixed Search metadata contract is required')
const tabs = canonical.tabs.map(tab => ({ ...tab, statusValues: [...tab.statusValues] }))
/** @param {string} userId @param {string} role @returns {Schemas['SearchUserReference']} */
const userReference = (userId, role) => {
  const user = people.find(person => person.id === userId)
  if (!user) throw new Error('Only normalized synthetic Users are permitted')
  return { id: user.id, displayName: user.displayName, isActive: user.isActive, role }
}
/** @param {URLSearchParams} query @param {boolean} [enabled] @returns {Schemas['SearchResponse']} */
function searchRead(query, enabled = true) {
  const q = query.get('q') ?? '', term = q.trim().toLowerCase(), type = query.get('type'), selected = tabs.find(tab => tab.type === type)
  /** @param {string} field */
  const filterId = field => {
    const value = enabled ? query.get(field)?.trim() : null
    if (!value) return null
    if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value) || value === '00000000-0000-0000-0000-000000000000') throw new Error('Synthetic invalid ' + field)
    return value.toLowerCase()
  }
  const projectId = filterId('projectId'), disciplineId = filterId('disciplineId'), status = enabled ? query.get('status')?.trim() || null : null
  if (enabled && type && !selected) throw new Error('Synthetic invalid type')
  if (type && projectId && !selected?.project) throw new Error('Synthetic projectId filter is not applicable to ' + type)
  if (type && disciplineId && !selected?.discipline) throw new Error('Synthetic disciplineId filter is not applicable to ' + type)
  if (status && (!type || !selected?.status || !selected.statusValues.includes(status))) throw new Error('Synthetic status requires the explicit supported type')
  /** @type {Schemas['SearchScreenMetadata']} */
  const screen = { applied: { projectId, disciplineId, status }, tabs, projects: projects.slice(0, 200).map(project => ({ id: project.id, projectNumber: project.projectNumber, name: project.name })),
    projectsTruncated: true, disciplines: disciplines.slice(0, 200).map(discipline => ({ id: discipline.id, name: discipline.name, isActive: discipline.isActive })), disciplinesTruncated: true }
  if (!term) return { q, exact: null, groups: {}, counts: {}, ...(enabled ? { screen } : {}) }
  const hits = {
    projects: projects.filter(project => (project.projectNumber + ' ' + project.name).toLowerCase().includes(term) && (!projectId || project.id === projectId)
      && (!disciplineId || project.disciplineId === disciplineId) && (!status || type !== 'projects' || status === 'Active')).map(project => ({ id: project.id,
      projectNumber: project.projectNumber, name: project.name, status: 'Active', client: null, pm: actorFor().displayName, ...(enabled ? { person: userReference(ids.viewer, 'pm') } : {}) })),
    tasks: tasks.filter(task => (task.key + ' ' + task.name).toLowerCase().includes(term) && (!projectId || task.projectId === projectId)
      && (!disciplineId || task.disciplineId === disciplineId) && (!status || type !== 'tasks' || task.status === status)).map(task => {
      const project = projects.find(project => project.id === task.projectId), person = people.find(person => person.id === task.userId)
      if (!project || !person) throw new Error('Task references must resolve to actual synthetic records')
      return { id: task.id, key: task.key, name: task.name, projectNumber: project.projectNumber, status: task.status, dueDate: null, assignee: person.displayName,
        match: null, ...(enabled ? { person: userReference(task.userId, 'assignee') } : {}) }
    }),
    people: people.filter(person => [person.displayName, person.email, ...(enabled ? [person.jobTitle, disciplines.find(discipline => discipline.id === person.homeDisciplineId)?.name] : [])]
      .some(text => text?.toLowerCase().includes(term)) && (!disciplineId || person.homeDisciplineId === disciplineId)
      && (!status || type !== 'people' || (person.isActive ? 'Active' : 'Inactive') === status)).map(person => ({ id: person.id, displayName: person.displayName, email: person.email,
      jobTitle: person.jobTitle, isActive: person.isActive, canViewWork: person.canViewWork, ...(enabled ? { person: userReference(person.id, 'person') } : {}) })),
  }
  const limit = Number(query.get('limit') ?? 5), page = Number(query.get('page') ?? 1)
  const counts = Object.fromEntries(tabs.filter(tab => !type || tab.type === type).map(tab => [tab.type, tab.type === 'projects' ? hits.projects.length : tab.type === 'tasks' ? hits.tasks.length : tab.type === 'people' ? hits.people.length : 0]))
  const groups = Object.fromEntries(Object.entries(hits).filter(([group]) => !type || group === type).map(([group, rows]) => [group, rows.slice((page - 1) * limit, page * limit)]))
  return { q, exact: null, groups, counts, ...(enabled ? { screen } : {}) }
}
/** @param {string} userId @returns {Schemas['PersonPanelRead']} */
function personRead(userId) {
  const person = people.find(person => person.id === userId)
  if (!person) throw new Error('An item/project ID must never substitute for a User')
  const home = disciplines.find(discipline => discipline.id === person.homeDisciplineId)
  return { id: person.id, displayName: person.displayName, jobTitle: person.jobTitle, isActive: person.isActive, today: '2026-11-02', week: '2026-11-02', asOf: '2026-11-02T12:00:00Z',
    organisationFacts: { supervisor: null, office: null, homeDiscipline: home ? { id: home.id, name: home.name } : null, unit: null }, projects: { page: 1, pageSize: 25, total: 0, items: [] } }
}
module.exports = { ids, actorFor, reference, people, projects, tasks, searchRead, personRead }
