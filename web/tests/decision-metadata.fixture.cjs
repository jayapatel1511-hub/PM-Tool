// @ts-check
// Synthetic AC-SCR-69/84 UI evidence; provider tests own real authority, receipt, dates and persistence.
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */
const { meView, referenceData } = require('./me.fixture.cjs')
const { projectDetail, projectList, projectRow } = require('./projects.fixture.cjs')
const legacy = require('./decisions.fixture.cjs')
/** @param {number} value */
const id = value => '00000000-0000-0000-0000-' + String(value).padStart(12, '0')
const ids = { project: legacy.ids.project, pm: id(7100), requester: legacy.ids.requester, owner: id(7101), party: legacy.ids.party, otherParty: id(7102), unknownParty: id(7103), category: id(7110), inactive: id(7111), pending: id(7120) }
/** @type {Schemas['DecisionCategoryChoice'][]} */
const categories = [{ id: ids.category, name: 'Synthetic category', isActive: true }, { id: ids.inactive, name: 'Synthetic retired category', isActive: false }]
/** @type {Schemas['ExternalPartyRow'][]} */
const parties = [{ ...legacy.externalParty, name: 'Synthetic contact A', organisation: 'Synthetic client organisation' }, { ...legacy.externalParty, id: ids.otherParty, name: 'Synthetic contact B', organisation: 'Synthetic client organisation' }, { ...legacy.externalParty, id: ids.unknownParty, name: 'Synthetic unrecorded organisation', organisation: null }]
const project = projectDetail({ id: ids.project, projectNumber: 'P-MOCK', name: 'Synthetic decision project', projectManagerId: ids.pm, pmName: 'Synthetic recorder',
  permissions: { raiseRegister: { ok: true, reason: null }, manageExternalParties: { ok: true, reason: null }, edit: true } })
const projects = projectList([projectRow({ id: project.id, projectNumber: project.projectNumber, name: project.name })])
/** @param {boolean} [enabled] @param {boolean} [requester] @returns {Schemas['MeView']} */
const actorFor = (enabled = true, requester = false) => meView({ id: requester ? ids.requester : ids.pm, displayName: requester ? 'Synthetic requester' : 'Synthetic recorder', email: 'decision-viewer@example.test',
  flags: { register_screens: enabled, people_screens: true, work_screens: false, screen_patterns: false, project_screens: false, live_events: false, onboarding: false }, settings: { today: '2026-11-14' } })
/** @returns {{rows:Schemas['DecisionRow'][], metadata:Map<string,Schemas['DecisionMetadataDetail']>, applied:number, complete:boolean}} */
function state() {
  /** @type {Schemas['DecisionRow'][]} */
  const rows = [4, 10, 12].map((days, index) => ({ ...legacy.decisionRow, id: id(7200 + index), key: 'DEC-C' + index, subject: 'Synthetic client choice ' + index, status: 'Decided',
    ownerExternalPartyId: index === 1 ? ids.otherParty : ids.party, ownerName: index === 1 ? 'Synthetic contact B' : 'Synthetic contact A', ownerOrganisation: 'Synthetic client organisation',
    dateRequested: '2026-11-01', decisionDate: '2026-11-' + String(1 + days).padStart(2, '0'), decisionText: 'Synthetic client outcome', daysUntil: null, requestedByName: 'Synthetic requester' }))
  rows.push(...[4, 11].map((days, index) => ({ ...legacy.decisionRow, id: id(7210 + index), key: 'DEC-I' + index, subject: 'Synthetic internal choice ' + index, status: 'Decided',
    ownerUserId: ids.owner, ownerExternalPartyId: null, ownerName: 'Synthetic internal owner', ownerOrganisation: null, ownerIsClient: false, dateRequested: '2026-11-01', decisionDate: '2026-11-' + String(1 + days).padStart(2, '0'), decisionText: 'Synthetic internal outcome', daysUntil: null })))
  rows.push({ ...legacy.decisionRow, id: ids.pending, key: 'DEC-PENDING', subject: 'Synthetic pending decision', ownerUserId: ids.owner, ownerExternalPartyId: null, ownerName: 'Synthetic internal owner', ownerOrganisation: null, ownerIsClient: false, dateRequested: '2026-11-01', requiredByDate: '2026-11-20', originalRequiredByDate: '2026-11-20', daysUntil: 6 })
  rows.push({ ...legacy.decisionRow, id: id(7220), key: 'DEC-UNKNOWN-DATE', subject: 'Synthetic unknown outcome date', status: 'Decided', decisionDate: null, dateRequested: '2026-11-01', decisionText: 'Legacy recorded outcome', daysUntil: null },
    { ...legacy.decisionRow, id: id(7221), key: 'DEC-UNKNOWN-ORG', subject: 'Synthetic unknown owner organisation', status: 'Decided', ownerExternalPartyId: ids.unknownParty, ownerOrganisation: null, decisionDate: '2026-11-05', dateRequested: '2026-11-01', decisionText: 'Legacy unclassified outcome', daysUntil: null })
  const metadata = new Map(rows.map(row => [row.id, { categoryId: ids.inactive, optionsConsidered: ['Synthetic option A', 'Synthetic option B'], rationale: row.status === 'Decided' ? 'Synthetic original rationale' : null, canEditRationale: false, correctionReasonRequired: false }]))
  return { rows, metadata, applied: 0, complete: false }
}
/** @param {ReturnType<typeof state>} source @param {URLSearchParams} query @param {boolean} enabled @returns {Schemas['DecisionRow'][]} */
function rows(source, query, enabled) {
  const status = query.get('status'), owner = query.get('ownerId'), term = query.get('q')?.toLowerCase()
  return source.rows.filter(row => (!status || status.split(',').includes(row.status)) && (!owner || [row.ownerUserId, row.ownerExternalPartyId].includes(owner))
    && (!term || (row.subject + ' ' + row.key).toLowerCase().includes(term))).map(row => {
    const value = { ...row }
    if (enabled) { const m = source.metadata.get(row.id); value.metadata = { category: categories.find(category => category.id === m?.categoryId) ?? null, optionCount: m?.optionsConsidered?.length ?? null, rationaleRecorded: !!m?.rationale } }
    else delete value.metadata
    return value
  })
}
/** @param {ReturnType<typeof state>} source @param {string} value @param {Schemas['MeView']} actor @returns {Schemas['DecisionDetailResponse']} */
function detail(source, value, actor) {
  const row = rows(source, new URLSearchParams(), actor.flags.register_screens === true).find(row => row.id === value)
  if (!row) throw new Error('Unknown synthetic Decision')
  const canDecide = actor.id === ids.pm || !source.complete && actor.id === row.ownerUserId
  const result = { ...legacy.decisionDetail, decision: row, project: { id: project.id, projectNumber: project.projectNumber, name: project.name, status: source.complete ? 'Complete' : 'Active' },
    decidedBy: row.status === 'Decided' ? 'Synthetic recorder' : null, permissions: { edit: { ok: !source.complete || actor.id === ids.pm, reason: null }, comment: true,
      transitions: row.status === 'Pending' ? [{ to: 'Decided', ok: canDecide, reason: canDecide ? null : 'Synthetic outcome permission refused' }] : [] } }
  if (actor.flags.register_screens === true) return { ...result, metadata: { categoryId: null, optionsConsidered: null, rationale: null, ...source.metadata.get(row.id), canEditRationale: row.status === 'Decided' && canDecide, correctionReasonRequired: source.complete } }
  return result
}
/** @param {ReturnType<typeof state>} source @returns {Schemas['DecisionScreenRead']} */
function screen(source) {
  const completed = source.rows.filter(row => row.status === 'Decided')
  let invalidDateCount = 0, unattributedCount = 0
  /** @type {Map<string,{ownerKind:string,organisation:string,days:number[]}>} */
  const samples = new Map()
  for (const row of completed) {
    const from = Date.parse(row.dateRequested), through = Date.parse(row.decisionDate ?? '')
    if (!Number.isFinite(through) || !Number.isFinite(from) || through < from || through > Date.parse('2026-11-14')) { invalidDateCount++; continue }
    const organisation = row.ownerUserId && !row.ownerExternalPartyId ? 'Internal' : !row.ownerUserId ? parties.find(party => party.id === row.ownerExternalPartyId)?.organisation?.trim() : null
    if (!organisation) { unattributedCount++; continue }
    const ownerKind = row.ownerUserId ? 'Internal' : 'External', key = JSON.stringify([ownerKind, organisation]), sample = samples.get(key) ?? { ownerKind, organisation, days: [] }
    sample.days.push((through - from) / 86400000); samples.set(key, sample)
  }
  const groups = [...samples.values()].map(sample => { const days = sample.days.sort((a, b) => a - b), midpoint = Math.floor(days.length / 2)
    return { ownerKind: sample.ownerKind, organisation: sample.organisation, decidedCount: days.length, medianDays: days.length % 2 ? days[midpoint] : (days[midpoint - 1] + days[midpoint]) / 2 } })
  return { asOf: '2026-11-14T14:00:00Z', categories, lag: { basis: 'CurrentRegisteredValuesAllDecidedInProject', total: completed.length, attributed: groups.reduce((sum, group) => sum + group.decidedCount, 0), invalidDateCount, unattributedCount, groups } }
}
/** @param {string} user @returns {Schemas['PersonPanelRead']} */
const person = user => ({ id: user, displayName: user === ids.requester ? 'Synthetic requester' : 'Synthetic internal owner', isActive: true, jobTitle: 'Synthetic designer', today: '2026-11-14', week: '2026-11-09', asOf: '2026-11-14T14:00:00Z',
  organisationFacts: { homeDiscipline: null, office: null, supervisor: null, unit: null }, projects: { page: 1, pageSize: 25, total: 0, items: [] } })
module.exports = { ids, categories, parties, project, projects, actorFor, reference: referenceData(), state, rows, detail, screen, person }
