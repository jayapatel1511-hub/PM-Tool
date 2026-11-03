// @ts-check
/** Synthetic User identities and source records only. */
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */
const { meView, referenceData } = require('./me.fixture.cjs')
/** @param {number} n */
const id = n => '00000000-0000-0000-0000-' + String(n).padStart(12, '0')
const ids = { viewer: id(9100), source: id(9101), sameName: id(9102), destinationB: id(9103), destinationC: id(9104), noWork: id(9105),
  project: id(10), task: id(1), otherTask: id(9121), missingMetadata: id(9122) }
const people = [
  { id: ids.source, displayName: 'Synthetic same name', isActive: false },
  { id: ids.sameName, displayName: 'Synthetic same name', isActive: true },
  { id: ids.destinationB, displayName: 'Synthetic destination B', isActive: true },
  { id: ids.destinationC, displayName: 'Synthetic destination C', isActive: true },
  { id: ids.noWork, displayName: 'Synthetic person without work access', isActive: true },
]
const actor = meView({ id: ids.viewer, displayName: 'Synthetic Search viewer', email: 'search-viewer@example.test', capabilities: { staff: true, admin: true, workload: false },
  flags: { visual_system: true, people_screens: true, capacity_screens: false, org_structure: false, screen_patterns: false, project_screens: false,
    work_screens: false, live_events: false, onboarding: false, home_overview: false }, settings: { today: '2026-10-31' } })
const reference = referenceData()
/** @param {string} userId @param {string} role @returns {Schemas['SearchUserReference']} */
const personRef = (userId, role) => {
  const p = people.find(p => p.id === userId)
  if (!p) throw new Error('Only actual fixture Users can be Search references')
  return { id: p.id, displayName: p.displayName, isActive: p.isActive, role }
}
/** @type {Schemas['SearchGroups']} */
const groups = {
  // Identical item GUIDs across groups deliberately exercise source-qualified focus IDs.
  projects: Array.from({ length: 5 }, (_, n) => ({ id: n ? id(9200 + n) : ids.task, projectNumber: n ? 'P-SYN-' + n : 'P-MOCK',
    name: 'Synthetic project ' + n, status: 'Active', client: 'Synthetic client', pm: people[0].displayName, person: personRef(ids.source, 'pm') })),
  tasks: Array.from({ length: 51 }, (_, n) => ({ id: n ? id(9300 + n) : ids.task, key: 'T-SYN-' + n, name: 'Synthetic task ' + n, projectNumber: 'P-MOCK',
    status: 'Ready', assignee: people[n % 2].displayName, dueDate: '2026-11-06', match: null, person: personRef(n % 2 ? ids.sameName : ids.source, 'assignee') })),
  deliverables: [{ id: id(9401), key: 'D-SYN', name: 'Synthetic drawing', projectNumber: 'P-MOCK', status: 'Draft', dueDate: null, owner: people[1].displayName,
    match: null, person: personRef(ids.sameName, 'owner') }],
  handoffs: [{ id: id(9402), key: 'H-SYN', name: 'Synthetic receiving handoff', projectNumber: 'P-MOCK', status: 'Requested', dueDate: '2026-11-06',
    owner: people[1].displayName, person: personRef(ids.sameName, 'receivingOwner') }],
  submissions: [{ id: id(9403), key: 'SUB-SYN', name: 'Synthetic submission', projectNumber: 'P-MOCK', dueDate: '2026-11-06', owner: people[0].displayName,
    person: personRef(ids.source, 'coordinator') }],
  allocations: [{ id: id(9404), name: people[1].displayName + ' · Combined allocation title stays intact', projectNumber: 'P-MOCK', status: 'Confirmed', dueDate: '2026-11-06',
    person: personRef(ids.sameName, 'person') }],
  'design-basis': [{ id: id(9405), key: 'B-SYN', name: 'Synthetic design basis', projectNumber: 'P-MOCK', owner: people[1].displayName, person: personRef(ids.sameName, 'owner') }],
  constraints: [{ id: id(9406), key: 'WC-SYN', name: 'Synthetic removal constraint', projectNumber: 'P-MOCK', status: 'Open', dueDate: '2026-11-06',
    owner: people[0].displayName, person: personRef(ids.source, 'removalOwner') }],
  commitments: [{ id: id(9407), key: 'CT-SYN', name: 'Synthetic promised output', projectNumber: 'P-MOCK', status: 'Promised', dueDate: '2026-11-06',
    owner: people[1].displayName, person: personRef(ids.sameName, 'performer') }],
  planning: [{ id: id(9408), name: people[0].displayName + ' · Combined planning title stays intact', personId: ids.source, status: 'Published',
    dueDate: '2026-11-06', startWeek: '2026-11-02', person: personRef(ids.source, 'person') }],
  comments: [{ id: id(9409), itemId: ids.task, itemType: 'Task', key: 'T-SYN-0', name: 'Synthetic commented task', projectNumber: 'P-MOCK',
    author: people[1].displayName, createdAt: '2026-10-31T16:00:00Z', match: 'Synthetic plain mention stays text', person: personRef(ids.sameName, 'author') }],
  people: people.map(p => ({ ...p, email: p.id + '@example.test', jobTitle: 'Synthetic designer', canViewWork: p.id !== ids.noWork, person: personRef(p.id, 'person') })),
  decisions: [{ id: ids.missingMetadata, key: 'DEC-SYN', name: 'Synthetic record without typed person', projectNumber: 'P-MOCK', status: 'Pending', requiredByDate: '2026-11-06' }],
}
/** Search must not guess a person from a name when optional metadata is absent. */
groups.tasks?.push({ id: ids.missingMetadata, key: 'T-NO-USER', name: 'Synthetic item without User metadata', projectNumber: 'P-MOCK', status: 'Ready',
  assignee: people[0].displayName, dueDate: null, match: null })
/** Same applicability and closed values as SearchScreenReads/SearchScreenTabs; existing legacy hit strings are untouched.
 * This fixture verifies Person source preservation, not filtered cohorts. Dedicated S46/API tests own filtering evidence.
 * @type {Schemas['SearchScreenTab'][]} */
const screenTabs = [
  { type: 'projects', project: true, status: true, discipline: true, statusValues: ['Setup', 'Active', 'On Hold', 'Complete', 'Archived', 'Cancelled'] },
  { type: 'tasks', project: true, status: true, discipline: true, statusValues: ['Not Started', 'In Progress', 'Ready for Review', 'In Review', 'Revision Required', 'Complete', 'On Hold', 'Cancelled'] },
  { type: 'deliverables', project: true, status: true, discipline: true, statusValues: ['Not Started', 'In Progress', 'In Review', 'Revision Required', 'Ready to Issue', 'Issued', 'Accepted', 'On Hold', 'Cancelled'] },
  { type: 'milestones', project: true, status: true, discipline: true, statusValues: ['On Track', 'At Risk', 'Overdue', 'Complete', 'Cancelled'] },
  { type: 'decisions', project: true, status: true, discipline: false, statusValues: ['Pending', 'Under Review', 'Decided', 'Deferred', 'Cancelled'] },
  { type: 'handoffs', project: true, status: true, discipline: true, statusValues: ['Draft', 'Submitted', 'Clarification Requested', 'Returned', 'Accepted', 'Incorporated', 'Cancelled'] },
  { type: 'reviews', project: true, status: true, discipline: true, statusValues: ['Draft', 'In Review', 'Changes Required', 'Approved', 'Superseded', 'Cancelled'] },
  { type: 'changes', project: true, status: true, discipline: true, statusValues: ['Draft', 'Open', 'Closed', 'Cancelled'] },
  { type: 'submissions', project: true, status: true, discipline: false, statusValues: ['Draft', 'Checking', 'Ready', 'Issued', 'Superseded', 'Cancelled'] },
  { type: 'allocations', project: true, status: true, discipline: false, statusValues: ['Proposed', 'Confirmed', 'Declined', 'Cancelled', 'Completed'] },
  { type: 'design-basis', project: true, status: true, discipline: true, statusValues: ['Proposed', 'Confirmed', 'Superseded', 'Withdrawn'] },
  { type: 'constraints', project: true, status: true, discipline: true, statusValues: ['Open', 'Resolution Proposed', 'Verified Removed', 'Cancelled'] },
  { type: 'commitments', project: true, status: true, discipline: true, statusValues: ['Proposed', 'Committed', 'Met', 'Met, awaiting confirmation', 'Not Met', 'Withdrawn'] },
  { type: 'comments', project: true, status: false, discipline: false, statusValues: [] },
  { type: 'planning', project: true, status: true, discipline: true, statusValues: ['Self', 'Draft', 'Published', 'Confirmed'] },
  { type: 'people', project: false, status: true, discipline: true, statusValues: ['Active', 'Inactive'] },
]
/** @returns {Schemas['SearchScreenMetadata']} */
function screenMetadata() {
  return { applied: { projectId: null, status: null, disciplineId: null }, tabs: screenTabs,
    projects: (groups.projects ?? []).map(project => ({ id: project.id, projectNumber: project.projectNumber, name: project.name }))
      .sort((a, b) => a.projectNumber.localeCompare(b.projectNumber) || a.id.localeCompare(b.id)), projectsTruncated: false,
    disciplines: reference.disciplines.map(discipline => ({ id: discipline.id, name: discipline.name, isActive: discipline.isActive })), disciplinesTruncated: false }
}
/** @param {URLSearchParams} query @param {boolean} [off] @param {boolean} [reverse] @returns {Schemas['SearchResponse']} */
function searchRead(query, off = false, reverse = false) {
  if (!off && ['projectId', 'status', 'disciplineId'].some(key => query.get(key)?.trim())) throw new Error('Person regression fixture does not simulate filtered Search cohorts; use the dedicated S46 evidence')
  const metadata = off ? {} : { screen: screenMetadata() }
  if (!off && !(query.get('q') ?? '').trim()) return { q: query.get('q') ?? '', exact: null, groups: {}, counts: {}, ...metadata }
  const type = query.get('type'), limit = Number(query.get('limit') ?? 5), page = Number(query.get('page') ?? 1)
  const counts = Object.fromEntries(Object.entries(groups).map(([group, rows]) => [group, rows?.length ?? 0]))
  const paged = Object.fromEntries(Object.entries(groups).filter(([group]) => !type || group === type).map(([group, source]) => {
    const rows = [...(source ?? [])]
    if (reverse) rows.reverse()
    return [group, rows.slice((page - 1) * limit, page * limit).map(row => {
      if (!off || !('person' in row)) return row
      const { person, ...legacy } = row
      return legacy
    })]
  }))
  return { q: query.get('q') ?? '', exact: null, groups: paged, counts, ...metadata }
}
/** @type {Schemas['OpenWorkView']} */
const openWork = { person: { id: ids.source, displayName: people[0].displayName, isActive: false, email: 'synthetic-source@example.test' }, items: [
  // One Task in two work roles: composite identity must stay kind:id.
  { kind: 'TaskAssignee', id: ids.task, key: 'T-SYN-0', name: 'Synthetic selected task', status: 'Ready', dueDate: '2026-11-06', projectId: ids.project, projectNumber: 'P-MOCK', projectName: 'Synthetic project' },
  { kind: 'TaskReviewer', id: ids.task, key: 'T-SYN-0', name: 'Synthetic selected task', status: 'In Review', dueDate: '2026-11-06', projectId: ids.project, projectNumber: 'P-MOCK', projectName: 'Synthetic project' },
  { kind: 'DeliverableOwner', id: ids.otherTask, key: 'D-SYN', name: 'Synthetic unselected drawing', status: 'Draft', dueDate: null, projectId: ids.project, projectNumber: 'P-MOCK', projectName: 'Synthetic project' },
] }
/** @param {string} userId @returns {Schemas['PersonPanelRead']} */
function personRead(userId) {
  const p = people.find(p => p.id === userId)
  if (!p) throw new Error('An item ID must never be used for a Person read')
  return { ...p, jobTitle: 'Visible synthetic Person facts', today: '2026-10-31', week: '2026-10-26', asOf: '2026-11-01T00:00:00Z',
    organisationFacts: { office: null, homeDiscipline: null, supervisor: null, unit: null }, projects: { page: 1, pageSize: 25, total: 0, items: [] } }
}
module.exports = { ids, people, actor, reference, groups, searchRead, openWork, personRead }
