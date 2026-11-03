// @ts-check
/** Synthetic residual S43 source records. Normal API authority is verified separately. */
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */
/** @typedef {{id:string,displayName:string,isActive:boolean}} KnownFileUser */
/** @typedef {Schemas['FileLinkRow'] & {addedByUser?:KnownFileUser|null}} FileWithAuthor */
const { projectRow, projectList } = require('./projects.fixture.cjs')
const { calendarEvent, calendarRange } = require('./calendar.fixture.cjs')
const { availabilityRead, availabilityPerson, availabilityDay } = require('./availability.fixture.cjs')
const { fileLink, fileProject, filesRead, homeRead } = require('./workspace.fixture.cjs')
const { surfacePerson } = require('./person-surface-completeness.fixture.cjs')
/** @param {number} n */
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const stamp = '2026-11-02T12:00:00Z'
const users = { first: id(7801), selected: id(7802), sameName: id(7803), denied: id(7804) }
const names = new Map([[users.first, 'Remaining Alex'], [users.selected, 'Remaining Blair'], [users.sameName, 'Remaining Alex'], [users.denied, 'Historical narrow author']])
/** @param {string} user @returns {KnownFileUser} */
const knownUser = user => ({ id: user, displayName: names.get(user) ?? 'Unknown fixture user', isActive: true })

/** @param {Schemas['ProjectDetail']} project */
function remainingFixture(project) {
  const rows = [projectRow({ id: project.id, projectNumber: project.projectNumber, name: 'Retained first project',
    pm: knownUser(users.first), client: { id: id(7811), name: 'Client one' }, rowVersion: 7 }),
  projectRow({ id: id(7812), projectNumber: 'P-REMAIN-2', name: 'Retained second project',
    pm: knownUser(users.first), client: { id: id(7813), name: 'Client two' }, rowVersion: 8 }),
  projectRow({ id: id(7814), projectNumber: 'P-REMAIN-3', name: 'Retained same-name PM project',
    pm: knownUser(users.sameName), rowVersion: 9 }),
  projectRow({ id: id(7815), projectNumber: 'P-REMAIN-4', name: 'Retained unavailable PM project',
    pm: { id: '00000000-0000-0000-0000-000000000000', displayName: 'Unassigned' }, rowVersion: 10 })]
  const events = [calendarEvent(id(7821), project.id, { title: 'Retained owner meeting', ownerId: users.first,
    owner: names.get(users.first), canEdit: false, projectNumber: project.projectNumber, start: '2026-11-02T09:00', end: '2026-11-02T10:00',
    startAt: stamp, endAt: '2026-11-02T13:00:00Z', description: 'A real event owner, independent of event.id.' }),
  calendarEvent(id(7822), project.id, { title: 'Retained second owner meeting', ownerId: users.first, owner: names.get(users.first),
    canEdit: true, projectNumber: project.projectNumber, start: '2026-11-03T09:00', end: '2026-11-03T10:00',
    startAt: '2026-11-03T12:00:00Z', endAt: '2026-11-03T13:00:00Z' })]
  /** @type {Schemas['MyStaffPerson']} */
  const staffPerson = { id: users.first, displayName: names.get(users.first) ?? '', isActive: true, jobTitle: 'Synthetic designer', officeId: null,
    projects: 1, open: 0, overdue: 0, blocked: 0, blocking: 0, deliverablesDue: 0, reviews: 0, reviewsStalled: 0,
    lastActivityAt: null, roles: { pm: 0, dl: 0, team: 1, reviewer: 0, viewer: 0 } }
  /** @type {Schemas['MyStaffList']} */
  const staff = { scope: 'all', canSeeAll: true, people: [staffPerson], asOf: stamp,
    tiles: { staff: 1, assignments: 1, reviewsStalled: 0, withBlocked: 0, withOverdue: 0 } }
  /** @type {Schemas['MyStaffAssignment']} */
  const assignment = { projectId: project.id, projectNumber: project.projectNumber, name: project.name, memberId: id(7831),
    reportedHealth: 'Green', roles: ['TeamMember'], leads: [], primaryDiscipline: project.disciplines[0]?.name ?? null,
    open: 0, overdue: 0, nextDue: null, status: 'Active', canRemove: true, rowVersion: 4, addedAt: stamp }
  /** @type {Schemas['MemberOpenItems']} */
  const openItems = { count: 0, tasks: [], deliverables: [] }
  /** @type {Schemas['AvailabilityLeave']} */
  const manualLeave = { id: id(7861), sourceKind: 'ManualHR', sourceSystem: 'Synthetic approved HR copy', externalId: 'Existing approved record',
    fromDate: '2026-11-05', throughDate: '2026-11-06', hoursPerDay: null, status: 'Approved', sourceUpdatedAt: stamp, importedAt: stamp, rowVersion: 3, canEdit: true }
  const away = availabilityRead({ from: '2026-11-01', through: '2026-11-30', canImport: false,
    people: [availabilityPerson({ id: users.first, displayName: names.get(users.first) ?? '',
      days: [availabilityDay({ date: '2026-11-02' })], leave: [manualLeave] }), availabilityPerson({ id: users.selected,
      displayName: names.get(users.selected) ?? '', days: [availabilityDay({ date: '2026-11-02' })] })] })
  /** The repeated author is real metadata, never the containing item's ID. @type {FileWithAuthor[]} */
  const authored = [users.first, users.selected, users.denied].map((user, i) => ({
    ...fileLink({ id: id(7851 + i), title: `Retained authored link ${i + 1}`, url: `https://example.test/retained/${i + 1}`,
      addedBy: names.get(user) ?? '', addedAt: stamp }), addedByUser: knownUser(user) }))
  /** @type {FileWithAuthor[]} */
  const historical = [fileLink({ id: id(7854), title: 'Retained legacy untyped author', url: 'https://example.test/legacy', addedBy: names.get(users.first) ?? '' }),
    { ...fileLink({ id: id(7855), title: 'Retained mismatched author', url: 'https://example.test/mismatch', addedBy: names.get(users.selected) ?? '' }), addedByUser: knownUser(users.first) },
    fileLink({ id: id(7856), title: 'Retained deleted author', url: 'https://example.test/deleted', addedBy: null })]
  const filler = Array.from({ length: 28 }, (_, i) => fileLink({ id: id(7900 + i), title: `Retained source context ${i + 1}`,
    url: `https://example.test/context/${i + 1}`, addedBy: null, addedAt: stamp }))
  const itemId = id(7841), allLinks = [...filler, ...authored, ...historical]
  /** @param {boolean} peopleOn @returns {Schemas['FilesRead']} */
  const files = peopleOn => filesRead({ total: allLinks.length, projects: [fileProject({ projectId: project.id,
    projectNumber: project.projectNumber, name: project.name, count: allLinks.length, items: [{ itemType: 'Task', itemId,
      key: 'T-REMAIN', name: 'Retained linked work', links: allLinks.map(link => {
        if (peopleOn) return link
        const { addedByUser: _omitted, ...plain } = /** @type {FileWithAuthor} */ (link); return plain
      }) }] })], ...(peopleOn ? { screen: {
        asOf: stamp, orgTimeZone: 'America/Halifax', weekFrom: '2026-11-02', weekThrough: '2026-11-08',
        applied: { disciplineId: null, addedById: null, addedThisWeek: false },
        disciplines: [], disciplinesTruncated: false,
        authors: [...new Map(authored.flatMap(link => link.addedByUser ? [[link.addedByUser.id, link.addedByUser]] : [])).values()],
        authorsTruncated: false,
        links: allLinks.map(link => ({ linkId: link.id, rowVersion: 1, canChange: false, discipline: null,
          referenceContext: { reference: null, target: null,
            comparison: { state: 'NotRecorded', declaredRevision: null, currentRevision: null } } })),
        missingDeliverables: { total: 0, truncated: false, items: [] },
      } } : {}) })
  /** @param {string} user @param {string} name @returns {Schemas['PersonPanelRead']} */
  const person = (user, name) => {
    const answer = surfacePerson(user, name)
    if (user === users.first) answer.organisationFacts.supervisor = { id: users.selected, name: names.get(users.selected) ?? '' }
    return answer
  }
  return { users, names, rows, projects: projectList(rows), events, calendar: calendarRange('2026-11-02', '2026-11-08', events),
    staff, staffPerson, assignment, openItems, away, manualLeave, files, itemId, authored, historical, person,
    candidates: [knownUser(users.first), knownUser(users.selected)], home: homeRead({ projectId: project.id, today: '2026-11-02' }) }
}
module.exports = { remainingFixture, id, stamp }
