// @ts-check
// Synthetic generated-contract Calendar sources; provider tests own real persistence, people scope and command authority.
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */
const { meView, referenceData, referenceDiscipline } = require('./me.fixture.cjs')
const { calendarDeadline, calendarEvent, calendarRange } = require('./calendar.fixture.cjs')
const { dateContextRead } = require('./dependencies.fixture.cjs')
const { savedView, viewList } = require('./views.fixture.cjs')
const { projectList, projectRow } = require('./projects.fixture.cjs')
/** @param {number} n */
const id = n => '00000000-0000-0000-0000-' + String(n).padStart(12, '0')
const ids = { viewer: id(9100), other: id(9101), project: id(9102), task: id(9103), drawing: id(9104), milestone: id(9105),
  undatedTask: id(9106), undatedDrawing: id(9107), civil: id(9108), electrical: id(9109), event: id(9110), successor: id(9111), child: id(9112), view: id(9113) }
/** @param {boolean} [enabled] @returns {Schemas['MeView']} */
const actorFor = (enabled = true) => meView({ id: ids.viewer, displayName: 'Synthetic Calendar PM', email: 'calendar-pm@example.test', systemRoles: ['ProjectManager'], capabilities: { workload: true },
  flags: { work_screens: enabled, date_context: false, capacity_screens: false, visual_system: false, org_structure: false, live_events: false, people_screens: false, data_entry_safety: true }, settings: { today: '2026-11-02' } })
const reference = referenceData({ disciplines: [referenceDiscipline(ids.civil, 'Civil'), referenceDiscipline(ids.electrical, 'Electrical')] })
const projects = projectList([projectRow({ id: ids.project, projectNumber: 'S-CAL', name: 'Synthetic Calendar project' })])
/** @type {Array<{fact: Schemas['CalendarScreenDeadline'], person: string | null, discipline: string | null}>} */
const sources = [
  { fact: { sourceType: 'Task', id: ids.task, key: 'S-CAL-T001', title: 'Synthetic due marker', projectId: ids.project, projectNumber: 'S-CAL', dueDate: '2026-11-02', startDate: '2026-11-01', rowVersion: 7, canMove: true, needsReason: true, moveReason: null }, person: ids.viewer, discipline: ids.civil },
  { fact: { sourceType: 'Deliverable', id: ids.drawing, key: 'S-CAL-D001', title: 'Synthetic drawing deadline', projectId: ids.project, projectNumber: 'S-CAL', dueDate: '2026-11-03', startDate: '2026-11-02', rowVersion: 4, canMove: true, needsReason: false, moveReason: null }, person: ids.other, discipline: ids.civil },
  { fact: { sourceType: 'Milestone', id: ids.milestone, key: 'S-CAL-M001', title: 'Synthetic ownerless milestone', projectId: ids.project, projectNumber: 'S-CAL', dueDate: '2026-11-04', startDate: null, rowVersion: 3, canMove: true, needsReason: true, moveReason: null }, person: null, discipline: null },
  { fact: { sourceType: 'Task', id: ids.undatedTask, key: 'S-CAL-T002', title: 'Synthetic undated task', projectId: ids.project, projectNumber: 'S-CAL', dueDate: null, startDate: null, rowVersion: 2, canMove: true, needsReason: false, moveReason: null }, person: ids.viewer, discipline: ids.electrical },
  { fact: { sourceType: 'Deliverable', id: ids.undatedDrawing, key: 'S-CAL-D002', title: 'Synthetic read-only undated drawing', projectId: ids.project, projectNumber: 'S-CAL', dueDate: null, startDate: null, rowVersion: 2, canMove: false, needsReason: false, moveReason: 'Synthetic read-only source' }, person: ids.other, discipline: ids.civil }
]
/** @type {Schemas['CalendarEventRow']} */
const event = calendarEvent(ids.event, ids.project, { title: 'Synthetic visible meeting', ownerId: ids.viewer, owner: 'Synthetic Calendar PM', projectNumber: 'S-CAL', projectName: 'Synthetic Calendar project',
  start: '2026-11-03T09:00', end: '2026-11-03T10:00', startAt: '2026-11-03T13:00:00Z', endAt: '2026-11-03T14:00:00Z', canEdit: false })
/** @param {URLSearchParams} params @param {boolean} [explicitEmpty] @returns {Schemas['CalendarRange']} */
function legacyRange(params, explicitEmpty = false) {
  const from = params.get('from') ?? '2026-11-02', to = params.get('to') ?? '2026-11-08'
  const selected = (params.get('types') ?? '').split(',').filter(Boolean), types = selected.length || explicitEmpty && params.has('types') ? selected : ['Deadline', 'Meeting', 'Site Work', 'Internal Task']
  /** @type {Schemas['CalendarEntry'][]} */
  const entries = sources.filter(row => row.fact.dueDate && row.fact.dueDate >= from && row.fact.dueDate <= to && types.includes('Deadline')).map(row => {
    if (!row.fact.dueDate) throw new Error('Dated projection requires its actual due date')
    return { ...calendarDeadline(row.fact.id, ids.project), sourceType: row.fact.sourceType, key: row.fact.key, title: row.fact.title, projectNumber: 'S-CAL', projectName: 'Synthetic Calendar project', date: row.fact.dueDate }
  })
  if (types.includes('Meeting') && event.start.slice(0, 10) >= from && event.start.slice(0, 10) <= to) entries.push(event)
  return calendarRange(from, to, entries)
}
/** @param {URLSearchParams} params @param {boolean} [awayAllowed] @returns {Schemas['CalendarScreenRead']} */
function read(params, awayAllowed = true) {
  const range = legacyRange(params, true), person = params.get('personId'), discipline = params.get('disciplineId')
  const wanted = params.has('types') ? (params.get('types') ?? '').split(',').filter(Boolean) : ['Deadline', 'Meeting', 'Site Work', 'Internal Task']
  const matching = sources.filter(row => (!person || row.person === person) && (!discipline || row.discipline === discipline))
  range.entries = range.entries.filter(entry => {
    if ('sourceType' in entry) return matching.some(row => row.fact.id === entry.id)
    return !discipline && (!person || person === ids.viewer)
  })
  const tray = wanted.includes('Deadline') ? matching.filter(row => !row.fact.dueDate).map(row => row.fact).sort((a, b) => a.sourceType < b.sourceType ? -1 : a.sourceType > b.sourceType ? 1 : a.key < b.key ? -1 : a.key > b.key ? 1 : 0) : []
  const page = Number(params.get('undatedPage') ?? 1), pageSize = Number(params.get('undatedPageSize') ?? 50)
  /** @type {Schemas['CalendarScreenAwayDay'][]} */
  const recorded = [{ personId: ids.viewer, person: 'Synthetic Calendar PM', date: '2026-11-04', state: 'Full', hours: 8, unverified: false },
    { personId: ids.viewer, person: 'Synthetic Calendar PM', date: '2026-11-05', state: 'Reduced', hours: 4, unverified: false },
    { personId: ids.viewer, person: 'Synthetic Calendar PM', date: '2026-11-06', state: 'Unknown', hours: null, unverified: true }]
  const show = params.get('showAway') === 'true', permitted = awayAllowed && (!person || person === ids.viewer)
  return { range, deadlines: matching.filter(row => range.entries.some(entry => 'id' in entry && entry.id === row.fact.id)).map(row => structuredClone(row.fact)),
    undated: { items: structuredClone(tray.slice((page - 1) * pageSize, page * pageSize)), page, pageSize, totalCount: tray.length },
    away: { state: !show ? 'Hidden' : permitted ? 'Available' : 'Unavailable', days: show && permitted && (!discipline || discipline === ids.civil) ? recorded.filter(row => row.date >= range.from && row.date <= range.to) : [] },
    people: [{ id: ids.viewer, name: 'Synthetic Calendar PM' }, { id: ids.other, name: 'Synthetic other visible owner' }],
    disciplines: [{ id: ids.civil, name: 'Civil' }, { id: ids.electrical, name: 'Electrical' }], asOf: '2026-11-02T12:00:00Z' }
}
/** @param {string} type @param {string} idValue */
function source(type, idValue) {
  const row = sources.find(row => row.fact.sourceType === type && row.fact.id === idValue)
  if (!row) throw new Error('Not a finite synthetic Calendar source')
  return row.fact
}
/** @param {string} type @param {string} idValue @param {string} date @returns {Schemas['CalendarScreenPreview']} */
function preview(type, idValue, date) {
  const fact = source(type, idValue)
  const context = dateContextRead({ successors: idValue === ids.task && date >= '2026-11-04' ? [{ id: ids.successor, key: 'S-CAL-T-NEXT', reason: { text: 'Synthetic successor would become Date Inconsistent.' } }] : [] },
    { tasks: idValue === ids.drawing && date < '2026-11-04' ? [{ id: ids.child, key: 'S-CAL-T-CHILD', reason: { text: 'Synthetic child would fall after the new due date.' } }] : undefined })
  const delta = (Date.parse(date + 'T00:00:00Z') - Date.parse((fact.dueDate ?? date) + 'T00:00:00Z')) / 86400000
  return { source: structuredClone(fact), dueDate: date, context: type === 'Milestone' ? null : context,
    milestone: type === 'Milestone' ? { milestone: { key: fact.key, name: fact.title, oldDate: fact.dueDate, newDate: date, slipDays: delta, delta },
      deliverables: [], tasks: [], inconsistent: date < '2026-11-03' ? [{ key: 'S-CAL-D001', name: 'Synthetic drawing deadline', dueDate: '2026-11-03' }] : [] } : null }
}
/** @param {string} type @param {string} idValue @param {string} date */
function commit(type, idValue, date) { const fact = source(type, idValue); fact.dueDate = date; fact.rowVersion++; return fact }
/** @type {Schemas['SavedViewRow'][]} */
const views = []
/** @param {Schemas['ViewBody']} body @returns {Schemas['ViewSaved']} */
function saveView(body) {
  views.push(savedView({ id: ids.view, name: body.name, scope: body.scope ?? 'Personal', projectId: body.projectId ?? null, params: body.params ?? {} }))
  return { id: ids.view, rowVersion: 1 }
}
module.exports = { ids, actorFor, reference, projects, source, sources, read, legacyRange, preview, commit, views, saveView, viewList }
