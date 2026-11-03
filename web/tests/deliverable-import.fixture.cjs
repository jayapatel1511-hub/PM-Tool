// @ts-check
/** Synthetic UI transport examples. These do not parse files or prove import atomicity. */
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */
const { createHash } = require('node:crypto')
const { meView, referenceData, referenceDeliverableType } = require('./me.fixture.cjs')
const { projectDetail, projectDiscipline } = require('./projects.fixture.cjs')
const { milestoneRow } = require('./milestones.fixture.cjs')
const { deliverableRow } = require('./deliverables.fixture.cjs')
/** @param {number} n */
const id = n => '00000000-0000-0000-0052-' + String(n).padStart(12, '0')
const ids = { viewer: id(100), owner: id(101), reviewer: id(102), discipline: id(110), alternateDiscipline: id(111), type: id(120), alternateType: id(121),
  project: '00000000-0000-0000-0000-000000000010', milestone: id(130) }
const actor = meView({ id: ids.viewer, displayName: 'Synthetic import reviewer', email: 'import-reviewer@example.test',
  capabilities: { createTask: true, workload: false }, flags: { register_screens: true, people_screens: true, visual_system: true, data_entry_safety: true,
    capacity_screens: false, org_structure: false, project_screens: false, delivery_screens: false, work_screens: false, screen_patterns: false,
    revision_check: false, live_events: false, onboarding: false, home_overview: false }, settings: { today: '2026-11-02' } })
const project = projectDetail({ id: ids.project, projectNumber: 'P-MOCK', name: 'Synthetic drawing import project', projectManagerId: ids.viewer,
  pmName: actor.displayName, rowVersion: 1, disciplines: [projectDiscipline({ id: ids.discipline, name: 'Synthetic civil', leadUserId: ids.owner, leadName: 'Synthetic drawing owner' }),
    projectDiscipline({ id: ids.alternateDiscipline, name: 'Synthetic alternate discipline', leadUserId: ids.owner, leadName: 'Synthetic drawing owner' })],
  permissions: { createDeliverableIn: [ids.discipline, ids.alternateDiscipline], isPm: true } })
const reference = referenceData({ deliverableTypes: [referenceDeliverableType(ids.type, 'Synthetic drawing'), referenceDeliverableType(ids.alternateType, 'Synthetic drawing package')] })
const milestone = milestoneRow({ id: ids.milestone, projectId: ids.project, key: 'P-MOCK-M0001', name: 'Synthetic drawing milestone', date: '2026-11-20' })
/** @type {Schemas['DrawingImportOptions']} */
const options = { projectRowVersion: 1, correctionReasonRequired: false, canImport: true,
  disciplines: [{ id: ids.discipline, name: 'Synthetic civil', canCreate: true }, { id: ids.alternateDiscipline, name: 'Synthetic alternate discipline', canCreate: true }],
  types: [{ id: ids.type, name: 'Synthetic drawing' }, { id: ids.alternateType, name: 'Synthetic drawing package' }] }
const marker = 'SYNTHETIC-IMPORT-MEMORY-ONLY-52'
const headers = ['title', 'client drawing number', 'discipline', 'type', 'owner email', 'reviewer email', 'milestone', 'start date', 'due date', 'revision', 'requires review', 'priority', 'description', 'source row key']
/** @param {string} text */
const cell = text => /[",\r\n]/.test(text) ? '"' + text.replaceAll('"', '""') + '"' : text
/** An actual 40-row CSV with optional one blank title; row identity is the physical source line. @param {boolean} [bad] */
const csv = (bad = false) => headers.join(',') + '\r\n' + Array.from({ length: 40 }, (_, n) => [bad && n === 12 ? '' : 'Synthetic drawing ' + (n + 1),
  'SYN-' + String(n + 1).padStart(3, '0'), '', '', '', 'synthetic-checker@example.test', milestone.key, '2026-11-02', '', 'A', 'true', 'High',
  marker + ', synthetic source description ' + (n + 1), 'SRC-' + String(n + 1).padStart(3, '0')].map(cell).join(',')).join('\r\n') + '\r\n'
const goodCsv = Buffer.from(csv()), badCsv = Buffer.from(csv(true))
// Intentionally MOCK-ONLY XLSX bytes: browser transport/sheet selection, never a real ZIP/parser fixture.
const mockXlsx = Buffer.concat([Buffer.from([0x50, 0x4b, 0x03, 0x04]), Buffer.from('SYNTHETIC MOCK XLSX TRANSPORT ONLY')])
const files = { good: { name: 'synthetic-corrected-40.csv', mimeType: 'text/csv', buffer: goodCsv }, bad: { name: 'synthetic-invalid-40.csv', mimeType: 'text/csv', buffer: badCsv },
  xlsx: { name: 'synthetic-ui-only.xlsx', mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', buffer: mockXlsx } }
/** @param {Schemas['DrawingImportPreviewBody']} body @param {number} sourceRow @returns {Schemas['DrawingImportValues']} */
function values(body, sourceRow) {
  const discipline = options.disciplines.find(d => d.id === body.defaults?.projectDisciplineId) ?? options.disciplines[0]
  const type = options.types.find(t => t.id === body.defaults?.deliverableTypeId) ?? options.types[0]
  return { name: 'Synthetic drawing ' + (sourceRow - 1), projectDisciplineId: discipline.id, projectDisciplineName: discipline.name,
    deliverableTypeId: type.id, deliverableTypeName: type.name, ownerId: ids.owner, ownerName: 'Synthetic drawing owner', reviewerId: ids.reviewer, reviewerName: 'Synthetic drawing checker',
    milestoneId: milestone.id, milestoneKey: milestone.key, startDate: '2026-11-02', dueDate: '2026-11-20', priority: 'High', revision: 'A', requiresReview: true,
    clientDrawingNumber: 'SYN-' + String(sourceRow - 1).padStart(3, '0'), description: marker + ', synthetic source description ' + (sourceRow - 1), ownerFromLead: true, dueFromMilestone: true }
}
/** Golden DTOs selected by exact source bytes; not a browser-side parser. @param {Schemas['DrawingImportPreviewBody']} body @param {number} [version] @returns {Schemas['DrawingImportPreview']} */
function preview(body, version = 1) {
  const xlsx = body.format === 'xlsx', chooseSheet = xlsx && !body.worksheet, invalid = body.contentBase64 === badCsv.toString('base64')
  const requireReason = body.sourceReference === 'requires-reason', missingReason = requireReason && !body.reason
  const rows = chooseSheet ? [] : Array.from({ length: 40 }, (_, n) => {
    const bad = invalid && n === 12
    return { sourceRow: n + 2, sourceRowInferred: false, hidden: false, classification: bad ? 'Error' : 'Create', sourceRowKey: 'SRC-' + String(n + 1).padStart(3, '0'),
      values: bad ? null : values(body, n + 2), errors: bad ? [{ code: 'required', field: 'title', message: 'Synthetic title is required.' }] : [], warnings: [] }
  })
  const diagnostics = chooseSheet ? [{ code: 'worksheet', severity: 1, message: 'Choose one visible worksheet.', sheet: null, sourceRow: null, column: null }]
    : missingReason ? [{ code: 'reason', severity: 1, message: 'Synthetic completed project requires a reason.', sheet: null, sourceRow: null, column: null }] : []
  const errors = (invalid ? 1 : 0) + (chooseSheet || missingReason ? 1 : 0)
  const canCommit = !chooseSheet && errors === 0
  return { format: body.format, sourceReference: body.sourceReference ?? null, selectedSheet: xlsx ? body.worksheet ?? null : null,
    contentSha256: createHash('sha256').update(Buffer.from(body.contentBase64, 'base64')).digest('hex'), previewFingerprint: canCommit ? createHash('sha256').update(JSON.stringify({ body, version })).digest('hex') : null,
    projectRowVersion: version, correctionReasonRequired: requireReason, complete: !chooseSheet, sheets: xlsx ? [
      { name: 'Cover', state: 'visible', part: 'xl/worksheets/sheet1.xml' }, { name: 'Drawings', state: 'visible', part: 'xl/worksheets/sheet2.xml' },
      { name: 'Archived', state: 'hidden', part: 'xl/worksheets/sheet3.xml' }] : [], diagnostics, rows, creates: chooseSheet ? 0 : invalid ? 39 : 40, errors,
    canCommit }
}
/** @param {number} [batch] @param {string} [projectDisciplineId] @returns {Schemas['DrawingImportCreated']} */
function created(batch = 0, projectDisciplineId = ids.discipline) {
  return { total: 40, rows: Array.from({ length: 40 }, (_, n) => ({ sourceRow: n + 2, id: id(1000 + batch * 40 + n), key: 'P-MOCK-D' + String(1 + batch * 40 + n).padStart(4, '0'),
    rowVersion: 1, projectDisciplineId, warnings: [] })) }
}
/** @param {Schemas['DrawingImportCreated']} receipt @param {Schemas['DrawingImportPreviewBody']} body @returns {Schemas['DeliverableRow'][]} */
function registerRows(receipt, body) {
  return receipt.rows.map(row => {
    const v = values(body, row.sourceRow)
    return deliverableRow({ id: row.id, projectId: ids.project, projectDisciplineId: row.projectDisciplineId, key: row.key, name: v.name,
      disciplineName: v.projectDisciplineName, deliverableTypeId: v.deliverableTypeId, typeName: v.deliverableTypeName,
      ownerId: v.ownerId, ownerName: v.ownerName, reviewerId: v.reviewerId, reviewerName: v.reviewerName, milestoneId: v.milestoneId,
      milestoneKey: v.milestoneKey, milestoneDate: v.dueDate, startDate: v.startDate, dueDate: v.dueDate, requiresReview: v.requiresReview,
      priority: v.priority, revision: v.revision, registerInfo: { clientDrawingNumber: v.clientDrawingNumber, numberPattern: null, numberMatchesPattern: null, currentIssue: null } })
  })
}
/** @param {string} userId @returns {Schemas['PersonPanelRead']} */
function personRead(userId) {
  if (![ids.owner, ids.reviewer].includes(userId)) throw new Error('Import person trigger must use a normalized actual User ID')
  return { id: userId, displayName: userId === ids.owner ? 'Synthetic drawing owner' : 'Synthetic drawing checker', jobTitle: 'Synthetic designer', isActive: true,
    today: '2026-11-02', week: '2026-11-02', asOf: '2026-11-02T12:00:00Z', organisationFacts: { supervisor: null, office: null, homeDiscipline: null, unit: null },
    projects: { page: 1, pageSize: 25, total: 1, items: [{ memberId: id(500), projectId: ids.project, projectNumber: project.projectNumber, projectName: project.name,
      projectStatus: 'Active', primaryDiscipline: null, roles: ['TeamMember'], leadOf: [], responsibilityNote: null }] } }
}
module.exports = { ids, actor, project, reference, milestone, options, marker, files, values, preview, created, registerRows, personRead }
