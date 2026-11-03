// @ts-check
// AC-SCR-77/84: synthetic UI cohorts only. Provider-backed FileScreen/DocumentLinkReference tests own authority and persistence.
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */
const { meView, referenceData, referenceDiscipline } = require('./me.fixture.cjs')
const { fileLink, fileProject, filesRead } = require('./workspace.fixture.cjs')
const { projectDetail, projectDiscipline, projectList, projectRow } = require('./projects.fixture.cjs')
const { deliverableRow, deliverableDetail } = require('./deliverables.fixture.cjs')
/** @param {number} value */
const id = value => '00000000-0000-0000-0000-' + String(value).padStart(12, '0')
const ids = { project: id(10), secondProject: id(20), crowdedProject: id(30), hiddenProject: id(40), viewer: id(6100), otherViewer: id(6101),
  author: id(6201), namesake: id(6202), registrar: id(6203), orphan: id(6299), civil: id(6301), electrical: id(6302), unknown: id(6399),
  declared: id(6401), equal: id(6402), unregistered: id(6403), parentAdded: id(6404), parentRemoved: id(6405), projectLink: id(6406), orphanLink: id(6407), unavailable: id(6408), secondLink: id(6409),
  deliverable: id(6501), equalDeliverable: id(6502), unknownDeliverable: id(6503), missing: id(6504), taskOnly: id(6505), deletedLink: id(6506), secondMissing: id(6507), secondDeliverable: id(6508),
  task: id(6601), addedTask: id(6602), removedTask: id(6603), orphanTask: id(6604), unavailableTask: id(6605),
  externalAuthor: id(10200), externalDiscipline: id(11200) }
/** @type {Schemas['DocumentLinkReferenceUser'][]} */
const people = [
  { id: ids.author, displayName: 'Synthetic same author', isActive: true },
  { id: ids.namesake, displayName: 'Synthetic same author', isActive: false },
  { id: ids.registrar, displayName: 'Synthetic registrar', isActive: true },
]
/** @param {boolean} [enabled] @param {string} [viewer] @returns {Schemas['MeView']} */
const actorFor = (enabled = true, viewer = ids.viewer) => meView({ id: viewer, displayName: 'Synthetic Files viewer', email: 'files-viewer@example.test',
  flags: { visual_system: true, people_screens: enabled, live_events: false, work_screens: false, project_screens: false, register_screens: false,
    capacity_screens: false, screen_patterns: false, baseline_history: false, revision_check: false, onboarding: false }, settings: { today: '2026-11-04' } })
const disciplines = [referenceDiscipline(ids.civil, 'Civil'), referenceDiscipline(ids.electrical, 'Electrical', { isActive: false })]
const reference = referenceData({ disciplines })
const projects = [
  projectDetail({ id: ids.project, projectNumber: 'P-MOCK', name: 'Synthetic valve project', disciplines: [projectDiscipline({ id: id(6311), disciplineId: ids.civil, name: 'Civil' })] }),
  projectDetail({ id: ids.secondProject, projectNumber: 'P-SECOND', name: 'Synthetic second valve project', disciplines: [projectDiscipline({ id: id(6321), disciplineId: ids.civil, name: 'Civil' })] }),
  projectDetail({ id: ids.crowdedProject, projectNumber: 'P-CROWDED', name: 'Synthetic capped choices' }),
]
const projectRows = projectList(projects.map(project => projectRow({ id: project.id, projectNumber: project.projectNumber, name: project.name })))
/** @param {string} value */
const actualPerson = value => people.find(person => person.id === value) ?? null
/** @param {string} declaration @param {string} [registrar] @returns {Schemas['DocumentLinkReferenceValue']} */
const registration = (declaration, registrar = ids.registrar) => ({ sourceSystem: 'Synthetic external register', stableSourceId: 'SYN-REF-' + declaration,
  declaredRevision: declaration, registeredBy: registrar, registeredByUser: actualPerson(registrar), registeredAt: '2026-11-04T13:00:00Z', registrationKind: 'Manual' })
/** @param {string} deliverableId @param {string|null} revision @returns {Schemas['DocumentLinkReferenceTarget']} */
const target = (deliverableId, revision) => ({ id: deliverableId, key: 'D-' + deliverableId.slice(-4), name: 'Synthetic valve register target', status: 'In Progress', currentRevision: revision, rowVersion: 17 })
/** @param {string} state @param {string|null} [declared] @param {string|null} [current] @param {Schemas['DocumentLinkReferenceTarget']|null} [linked] @returns {Schemas['DocumentLinkReferenceContext']} */
const context = (state, declared = null, current = null, linked = null) => ({ reference: declared ? registration(declared) : null, target: linked,
  comparison: { state, declaredRevision: declared, currentRevision: current } })
/** @param {string} linkId @param {string} title @param {string} authorId @param {Partial<Schemas['FileLinkRow']>} [extra] @returns {Schemas['FileLinkRow']} */
const row = (linkId, title, authorId, extra = {}) => fileLink({ id: linkId, title, url: 'https://example.test/synthetic/' + linkId,
  addedBy: actualPerson(authorId)?.displayName ?? null, addedByUser: actualPerson(authorId), addedAt: '2026-11-04T12:00:00Z', ...extra })
const facts = [
  { row: row(ids.declared, '01 Synthetic valve declared C', ids.author, { linkType: 'SharePoint' }), projectId: ids.project, itemType: 'Deliverable', itemId: ids.deliverable, disciplineId: ids.civil, authorId: ids.author, context: context('Different', 'C', 'D', target(ids.deliverable, 'D')), canChange: true },
  { row: row(ids.equal, '02 Synthetic valve matching D', ids.namesake, { linkType: 'SharePoint', addedAt: '2026-11-01T23:00:00Z' }), projectId: ids.project, itemType: 'Deliverable', itemId: ids.equalDeliverable, disciplineId: ids.civil, authorId: ids.namesake, context: context('Matches', 'D', 'D', target(ids.equalDeliverable, 'D')), canChange: false },
  { row: row(ids.unregistered, '03 Synthetic valve legacy', ids.author), projectId: ids.project, itemType: 'Deliverable', itemId: ids.unknownDeliverable, disciplineId: ids.civil, authorId: ids.author, context: context('NotRecorded'), canChange: false },
  { row: row(ids.parentAdded, '04 Synthetic valve parent added', ids.author), projectId: ids.project, itemType: 'Task', itemId: ids.addedTask, disciplineId: ids.civil, authorId: ids.author, context: context('TargetChanged', 'B'), canChange: false },
  { row: row(ids.parentRemoved, '05 Synthetic valve parent removed', ids.namesake), projectId: ids.project, itemType: 'Task', itemId: ids.removedTask, disciplineId: ids.civil, authorId: ids.namesake, context: context('TargetChanged', 'A'), canChange: false },
  { row: row(ids.projectLink, '06 Synthetic valve project reference', ids.author), projectId: ids.project, itemType: 'Project', itemId: ids.project, disciplineId: null, authorId: ids.author, context: context('NoDeliverable', 'P'), canChange: false },
  { row: row(ids.orphanLink, '07 Synthetic task-only valve folder', ids.orphan, { url: '\\\\files.test\\synthetic\\valve', linkType: 'Network Folder', isNetworkPath: true }), projectId: ids.project, itemType: 'Task', itemId: ids.orphanTask, disciplineId: ids.electrical, authorId: ids.orphan, context: context('NotRecorded'), canChange: false },
  { row: row(ids.unavailable, '08 Synthetic valve unavailable target', ids.namesake), projectId: ids.project, itemType: 'Task', itemId: ids.unavailableTask, disciplineId: ids.civil, authorId: ids.namesake, context: context('TargetUnavailable', 'X'), canChange: false },
  { row: row(ids.secondLink, '09 Synthetic valve current not recorded', ids.namesake), projectId: ids.secondProject, itemType: 'Deliverable', itemId: ids.secondDeliverable, disciplineId: ids.civil, authorId: ids.namesake, context: context('CurrentNotRecorded', 'E', null, target(ids.secondDeliverable, null)), canChange: false },
]
/** @param {string} value @param {string} projectId @param {string} name @param {string} [status] @returns {Schemas['FileScreenMissingDeliverable']} */
const missingRow = (value, projectId, name, status = 'Not Started') => {
  const project = projects.find(project => project.id === projectId)
  if (!project) throw new Error('Missing rows need an actual scoped project')
  return { id: value, projectId, projectNumber: project.projectNumber, projectName: project.name, key: 'D-' + value.slice(-4), name, status,
    disciplineId: ids.civil, disciplineName: 'Civil', currentRevision: value === ids.missing ? 'C' : null, rowVersion: 3, canAddLink: value === ids.missing }
}
const deliverables = [missingRow(ids.deliverable, ids.project, 'Synthetic valve linked drawing'), missingRow(ids.equalDeliverable, ids.project, 'Synthetic valve equal drawing'),
  missingRow(ids.unknownDeliverable, ids.project, 'Synthetic valve legacy drawing'), missingRow(ids.missing, ids.project, 'Synthetic valve missing drawing'),
  missingRow(ids.taskOnly, ids.project, 'Synthetic task-only valve drawing', 'On Hold'), missingRow(ids.deletedLink, ids.project, 'Synthetic valve deleted-link drawing', 'Ready to Issue'),
  missingRow(ids.secondMissing, ids.secondProject, 'Synthetic second valve missing drawing'), missingRow(ids.secondDeliverable, ids.secondProject, 'Synthetic valve source drawing'),
  missingRow(id(6591), ids.project, 'Synthetic valve issued drawing', 'Issued'), missingRow(id(6592), ids.project, 'Synthetic valve accepted drawing', 'Accepted'),
  missingRow(id(6593), ids.project, 'Synthetic valve cancelled drawing', 'Cancelled')]
/** @type {Schemas['FileScreenAuthorChoice'][]} */
const crowdedAuthors = Array.from({ length: 201 }, (_, index) => ({ id: id(10000 + index), displayName: 'Synthetic author ' + String(index).padStart(3, '0'), isActive: true }))
/** @type {Schemas['FileScreenDisciplineChoice'][]} */
const crowdedDisciplines = Array.from({ length: 201 }, (_, index) => ({ id: id(11000 + index), name: 'Synthetic discipline ' + String(index).padStart(3, '0'), isActive: true }))
const crowdedFacts = crowdedAuthors.map((author, index) => ({ row: fileLink({ id: id(12000 + index), title: 'Synthetic capped link ' + String(index).padStart(3, '0'),
  url: 'https://example.test/synthetic/capped/' + index, addedBy: author.displayName, addedByUser: author, addedAt: '2026-11-04T12:00:00Z' }),
  projectId: ids.crowdedProject, itemType: 'Task', itemId: id(13000 + index), disciplineId: crowdedDisciplines[index].id, authorId: author.id, context: context('NotRecorded'), canChange: false }))
const crowdedMissing = Array.from({ length: 2001 }, (_, index) => ({ ...missingRow(id(20000 + index), ids.crowdedProject, 'Synthetic capped missing ' + String(index).padStart(4, '0')),
  disciplineId: crowdedDisciplines[0].id, disciplineName: crowdedDisciplines[0].name, canAddLink: false }))
/** @param {URLSearchParams} query @param {boolean} enabled @returns {Schemas['FilesRead']} */
function read(query, enabled) {
  const requested = query.get('projects'), scoped = projects.filter(project => !requested || requested === 'all' || requested === 'mine' || requested.split(',').includes(project.id))
  const inScope = scoped.map(project => project.id), all = [...facts, ...crowdedFacts].filter(fact => inScope.includes(fact.projectId))
  const disciplineId = enabled ? query.get('disciplineId')?.trim() || null : null, addedById = enabled ? query.get('addedById')?.trim() || null : null
  const rawWeek = enabled ? query.get('addedThisWeek')?.trim() || null : null
  for (const [field, value] of [['disciplineId', disciplineId], ['addedById', addedById]])
    if (value && (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value) || value === id(0))) throw new Error('Synthetic invalid ' + field)
  if (rawWeek && !['true', 'false'].includes(rawWeek)) throw new Error('Synthetic invalid addedThisWeek')
  const term = (query.get('q') ?? '').trim().toLowerCase(), type = query.get('type'), addedThisWeek = rawWeek === 'true'
  const filtered = all.filter(fact => (!type || fact.row.linkType === type) && (!term || (fact.row.title + ' ' + fact.row.url).toLowerCase().includes(term))
    && (!disciplineId || fact.disciplineId === disciplineId) && (!addedById || fact.authorId === addedById)
    && (!addedThisWeek || fact.row.addedAt >= '2026-11-02T04:00:00Z' && fact.row.addedAt <= '2026-11-04T14:00:00Z')).sort((a, b) => a.row.title.localeCompare(b.row.title) || a.row.id.localeCompare(b.row.id))
  const shown = filtered.slice(0, 2000)
  const answer = filesRead({ total: shown.length, truncated: filtered.length > 2000, projects: scoped.flatMap(project => {
    const own = shown.filter(fact => fact.projectId === project.id)
    if (!own.length) return []
    const items = [...new Set(own.map(fact => fact.itemType + ':' + fact.itemId))].map(key => {
      const subset = own.filter(fact => fact.itemType + ':' + fact.itemId === key), first = subset[0]
      return { itemType: first.itemType, itemId: first.itemId, key: first.itemType === 'Project' ? project.projectNumber : first.itemType.slice(0, 1) + '-' + first.itemId.slice(-4), name: 'Synthetic valve linked item', links: subset.map(fact => {
        if (enabled) return fact.row
        const { addedByUser: _hidden, ...legacy } = fact.row; return legacy
      }) }
    }).sort((a, b) => (a.itemType === 'Project' ? 0 : 1) - (b.itemType === 'Project' ? 0 : 1) || a.key.localeCompare(b.key))
    return [fileProject({ projectId: project.id, projectNumber: project.projectNumber, name: project.name, items, count: own.length })]
  }) })
  if (!enabled) return answer
  const authors = [...people, ...crowdedAuthors].filter(person => all.some(fact => fact.authorId === person.id)).sort((a, b) => a.displayName.localeCompare(b.displayName) || a.id.localeCompare(b.id))
  const allDisciplines = [...disciplines, ...crowdedDisciplines].filter(discipline => all.some(fact => fact.disciplineId === discipline.id) || [...deliverables, ...crowdedMissing].some(item => inScope.includes(item.projectId) && item.disciplineId === discipline.id)).sort((a, b) => a.name.localeCompare(b.name) || a.id.localeCompare(b.id))
  const missing = [...deliverables, ...crowdedMissing].filter(item => inScope.includes(item.projectId) && !['Issued', 'Accepted', 'Cancelled'].includes(item.status)
    && !all.some(fact => fact.itemType === 'Deliverable' && fact.itemId === item.id)
    && (!disciplineId || item.disciplineId === disciplineId) && (!term || (item.key + ' ' + item.name).toLowerCase().includes(term))).sort((a, b) => a.projectNumber.localeCompare(b.projectNumber) || a.key.localeCompare(b.key) || a.id.localeCompare(b.id))
  answer.screen = { asOf: '2026-11-04T14:00:00Z', orgTimeZone: 'America/Halifax', weekFrom: '2026-11-02', weekThrough: '2026-11-08',
    applied: { disciplineId, addedById, addedThisWeek }, disciplines: allDisciplines.slice(0, 200).map(choice => ({ id: choice.id, name: choice.name, isActive: choice.isActive })), disciplinesTruncated: allDisciplines.length > 200,
    authors: authors.slice(0, 200), authorsTruncated: authors.length > 200,
    links: shown.map(fact => ({ linkId: fact.row.id, rowVersion: 5, canChange: fact.canChange, discipline: allDisciplines.find(choice => choice.id === fact.disciplineId) ?? null, referenceContext: fact.context })),
    missingDeliverables: { total: missing.length, truncated: missing.length > 2000, items: missing.slice(0, 2000) } }
  return answer
}
/** @param {string} userId @returns {Schemas['PersonPanelRead']} */
function personRead(userId) {
  const person = [...people, ...crowdedAuthors].find(person => person.id === userId)
  if (!person) throw new Error('Not an actual synthetic User')
  return { id: person.id, displayName: person.displayName, isActive: person.isActive, jobTitle: 'Synthetic designer', today: '2026-11-04', week: '2026-11-02', asOf: '2026-11-04T14:00:00Z',
    organisationFacts: { homeDiscipline: null, office: null, supervisor: userId === ids.author ? { id: ids.registrar, name: 'Synthetic registrar' } : null, unit: null }, projects: { page: 1, pageSize: 25, total: 0, items: [] } }
}
/** @param {string} deliverableId @returns {Schemas['DeliverableDetail']} */
function deliverableRead(deliverableId) {
  const item = deliverables.find(item => item.id === deliverableId)
  if (!item) throw new Error('Unknown synthetic Deliverable')
  return deliverableDetail({ deliverable: deliverableRow({ id: item.id, projectId: item.projectId, key: item.key, name: item.name, projectDisciplineId: id(item.projectId === ids.project ? 6311 : 6321),
    status: item.status, revision: item.currentRevision, rowVersion: item.rowVersion, tasks: [] }), project: { id: item.projectId, projectNumber: item.projectNumber, name: item.projectName, status: 'Active' } })
}
module.exports = { ids, people, actorFor, reference, projects, projectRows, facts, deliverables, read, personRead, deliverableRead }
