// @ts-check
// AC-SCR-70/81/84 synthetic generated-contract UI data. API tests own persistence/authority/copy semantics.
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */
const { meView, referenceData, referenceDiscipline, referenceClient, referenceOffice } = require('./me.fixture.cjs')
const { templateRead, templateRow, templateList, templatePreview } = require('./templates.fixture.cjs')
const { projectDetail, projectRow, projectList } = require('./projects.fixture.cjs')
/** @param {number} value */
const id = value => '00000000-0000-0000-0000-' + String(value).padStart(12, '0')
const ids = { viewer: id(7300), family: id(7301), source: id(7303), latest: id(7305), draft: id(7306), civil: id(7310),
  legacyDraft: id(7307), unavailableDraft: id(7308), legacyFamily: id(7380), unavailableFamily: id(7381),
  riskOne: id(7320), riskTwo: id(7321), oldRisk: id(7322), task: id(7330), project: id(7340), noSourceProject: id(7341), unavailableProject: id(7342),
  sourceRiskOne: id(7323), latestRiskOne: id(7324), latestRiskTwo: id(7325), sourceTask: id(7331),
  client: id(7350), office: id(7351), sourceMilestone: id(7360), draftMilestone: id(7361), latestMilestone: id(7362) }
/** @param {boolean} [risk] @param {boolean} [people] @returns {Schemas['MeView']} */
const actorFor = (risk = true, people = true) => meView({ id: ids.viewer, displayName: 'Synthetic template editor', email: 'template-editor@example.test',
  isTemplateEditor: true, systemRoles: ['ProjectManager'], capabilities: { templates: true, createProject: true }, flags: { register_screens: risk, people_screens: people, template_roles: false, template_working_days: false,
    data_entry_safety: true, visual_system: false, live_events: false, screen_patterns: false, project_screens: false, org_structure: false, revision_check: false }, settings: { today: '2026-11-02' } })
const reference = referenceData({ disciplines: [referenceDiscipline(ids.civil, 'Civil')], clients: [referenceClient(ids.client, 'Synthetic client')], offices: [referenceOffice(ids.office, 'Synthetic office')] })
/** @param {string} ref @returns {Schemas['MilestoneIn']} */
const milestone = ref => ({ ref, name: 'Synthetic baseline milestone', milestoneType: 'Kickoff', anchor: 'ProjectStart', offset: 0, completesPhaseId: null, isClientFacing: false })
/** @type {Schemas['TemplateRiskIn'][]} */
const seeds = [{ ref: ids.riskOne, title: 'Synthetic first risk', probability: 2, impact: 3, mitigation: 'Synthetic mitigation', triggerIndicator: 'Synthetic trigger' },
  { ref: ids.riskTwo, title: 'Added risk', probability: 1, impact: 2, mitigation: null, triggerIndicator: null }]
/** @type {Schemas['TemplateDraftChange'][]} */
const changes = [{ kind: 'Risk', change: 'Added', ref: 'new:' + ids.riskTwo, beforeName: null, afterName: 'Added risk', fields: [{ field: 'name', before: null, after: 'Added risk' }] },
  { kind: 'Risk', change: 'Removed', ref: ids.oldRisk, beforeName: 'Removed risk', afterName: null, fields: [{ field: 'name', before: 'Removed risk', after: null }] },
  { kind: 'Task', change: 'Changed', ref: ids.sourceTask, beforeName: 'Original task', afterName: 'Changed task', fields: [{ field: 'name', before: 'Original task', after: 'Changed task' }] },
  ...Array.from({ length: 48 }, (_, index) => ({ kind: 'Task', change: 'Changed', ref: id(7600 + index), beforeName: 'Original synthetic paged task ' + index,
    afterName: 'Changed synthetic paged task ' + index, fields: [{ field: 'name', before: 'Original synthetic paged task ' + index, after: 'Changed synthetic paged task ' + index }] }))]
/** @type {Schemas['TemplateRead']} */
let draft = templateRead({ id: ids.draft, familyId: ids.family, name: 'Synthetic template draft', rowVersion: 7,
  disciplines: [{ disciplineId: ids.civil, isDefaultIncluded: true, templateDisciplineId: id(7311) }],
  milestones: [milestone(ids.draftMilestone)],
  tasks: [{ ref: ids.task, disciplineId: ids.civil, deliverableRef: null, name: 'Changed task', description: null, requiresReview: false, priority: 'Medium', estimatedHours: null, offset: null, assignTo: 'Unassigned' },
    ...Array.from({ length: 48 }, (_, index) => ({ ref: id(7500 + index), disciplineId: ids.civil, deliverableRef: null, name: 'Changed synthetic paged task ' + index,
      description: null, requiresReview: false, priority: 'Medium', estimatedHours: null, offset: null, assignTo: 'Unassigned' }))],
  risks: seeds })
const sourceTasks = draft.tasks.map((task, index) => ({ ...task, ref: index === 0 ? ids.sourceTask : id(7600 + index - 1),
  name: index === 0 ? 'Original task' : 'Original synthetic paged task ' + (index - 1) }))
const templateIds = [ids.source, ids.latest, ids.draft, ids.legacyDraft, ids.unavailableDraft]
/** @param {string} selected @param {boolean} risk @param {boolean} people @param {number} [page] @returns {Schemas['TemplateRead']} */
function read(selected, risk, people, page = 1) {
  if (!templateIds.includes(selected)) throw new Error('Not a finite synthetic template version')
  const version = selected === ids.source ? 3 : 5
  const source = selected === ids.source
  const unknown = selected === ids.legacyDraft || selected === ids.unavailableDraft
  const result = structuredClone(selected === ids.draft || unknown ? draft : templateRead({ id: selected, familyId: ids.family, name: selected === ids.source ? 'Synthetic source' : 'Synthetic latest',
    status: selected === ids.source ? 'Retired' : 'Published', version, publishedAt: source ? '2026-10-01T12:00:00Z' : '2026-11-02T12:00:00Z', canEdit: false,
    risks: source ? [{ ...seeds[0], ref: ids.sourceRiskOne }, { ...seeds[1], ref: ids.oldRisk, title: 'Removed risk' }]
      : [{ ...seeds[0], ref: ids.latestRiskOne }, { ...seeds[1], ref: ids.latestRiskTwo }],
    disciplines: [{ disciplineId: ids.civil, isDefaultIncluded: true, templateDisciplineId: id(source ? 7312 : 7313) }],
    milestones: [milestone(source ? ids.sourceMilestone : ids.latestMilestone)],
    tasks: source ? sourceTasks : [] }))
  result.family = [{ id: ids.draft, version: null, status: 'Draft', publishedAt: null }, { id: ids.latest, version: 5, status: 'Published', publishedAt: '2026-11-02T12:00:00Z' },
    { id: ids.source, version: 3, status: 'Retired', publishedAt: '2026-10-01T12:00:00Z' }]
  if (unknown) {
    const base = selected === ids.legacyDraft ? 8800 : 8900
    result.id = selected; result.familyId = selected === ids.legacyDraft ? ids.legacyFamily : ids.unavailableFamily
    result.name = selected === ids.legacyDraft ? 'Synthetic legacy draft' : 'Synthetic unavailable draft'; result.rowVersion = 2
    result.family = [{ id: selected, version: null, status: 'Draft', publishedAt: null }]
    result.disciplines = result.disciplines.map(row => ({ ...row, templateDisciplineId: id(base + 1) }))
    result.milestones = result.milestones.map((row, index) => ({ ...row, ref: id(base + 20 + index) }))
    result.tasks = result.tasks.map((row, index) => ({ ...row, ref: id(base + 30 + index) }))
    result.risks = result.risks?.map((row, index) => ({ ...row, ref: id(base + 10 + index) }))
  }
  if (people) {
    result.family = result.family.map(row => ({ ...row, projects: row.id === ids.source ? 21 : 0 }))
    result.screen = { projects: selected === ids.source ? 21 : 0, draftDiff: result.status === 'Draft' ? { state: 'Available', asOf: '2026-11-02T12:00:00Z', draftRowVersion: result.rowVersion,
      source: { id: ids.source, name: 'Synthetic source', version: 3 }, counts: { added: changes.filter(row => row.change === 'Added').length, removed: changes.filter(row => row.change === 'Removed').length, changed: changes.filter(row => row.change === 'Changed').length }, changes: changes.slice((page - 1) * 50, page * 50), page, pageSize: 50, totalCount: changes.length,
      incompleteSections: ['Role', 'OffsetDayKind', ...(!risk ? ['Risk'] : [])] } : null }
    if (!risk && result.screen.draftDiff) {
      const visible = changes.filter(row => row.kind !== 'Risk'); result.screen.draftDiff = { ...result.screen.draftDiff, counts: { added: 0, removed: 0, changed: visible.length },
        changes: visible.slice((page - 1) * 50, page * 50), totalCount: visible.length }
    }
    if (result.screen.draftDiff && unknown) result.screen.draftDiff = { ...result.screen.draftDiff, state: selected === ids.legacyDraft ? 'NotRecorded' : 'Unavailable',
      source: null, counts: null, changes: [], totalCount: null }
  } else delete result.screen
  if (!risk) delete result.risks
  return result
}
/** @returns {Schemas['TemplateList']} */
function list() { return templateList([templateRow({ id: ids.draft, familyId: ids.family, name: draft.name, rowVersion: draft.rowVersion, milestones: draft.milestones.length, tasks: draft.tasks.length }),
  templateRow({ id: ids.latest, familyId: ids.family, name: 'Synthetic latest', version: 5, status: 'Published', milestones: 1 }),
  templateRow({ id: ids.source, familyId: ids.family, name: 'Synthetic source', version: 3, status: 'Retired', projects: 21, milestones: 1, tasks: 49 }),
  templateRow({ id: ids.legacyDraft, familyId: ids.legacyFamily, name: 'Synthetic legacy draft', rowVersion: 2, milestones: 1, tasks: 49 }),
  templateRow({ id: ids.unavailableDraft, familyId: ids.unavailableFamily, name: 'Synthetic unavailable draft', rowVersion: 2, milestones: 1, tasks: 49 })]) }
/** @param {string} selected @param {number} page @param {number} size @returns {Schemas['PageOfTemplateVersionProject']} */
function projects(selected, page, size) {
  const rows = selected === ids.source ? Array.from({ length: 21 }, (_, index) => ({ projectId: index === 0 ? ids.project : index === 20 ? ids.unavailableProject : id(7400 + index),
    projectNumber: index === 0 ? '250101' : 'P-SYN-' + index, name: 'Synthetic created project ' + index, status: index === 1 ? 'Archived' : 'Active', createdAt: '2026-10-01T12:00:00Z' })) : []
  return { items: rows.slice((page - 1) * size, page * size), page, pageSize: size, totalCount: rows.length }
}
/** @param {Schemas['StructureBody']} body @param {boolean} risks @returns {Schemas['TemplateRead']} */
function save(body, risks) {
  draft = { ...draft, rowVersion: draft.rowVersion + 1, name: body.header?.name ?? draft.name, description: body.header?.description ?? null,
    tasks: body.tasks, deliverables: body.deliverables, milestones: body.milestones, dependencies: body.dependencies,
    ...(risks && body.risks ? { risks: body.risks } : {}) }
  if (risks && body.risks?.[0]?.title !== seeds[0].title && !changes.some(row => row.kind === 'Risk' && row.ref === ids.sourceRiskOne))
    changes.push({ kind: 'Risk', change: 'Changed', ref: ids.sourceRiskOne, beforeName: seeds[0].title, afterName: body.risks?.[0]?.title ?? null,
      fields: [{ field: 'name', before: seeds[0].title, after: body.risks?.[0]?.title ?? null }] })
  const reply = read(ids.draft, risks, false)
  return reply
}
function advanceVersion() { draft = { ...draft, rowVersion: draft.rowVersion + 1 } }
/** @type {Schemas['TemplateProjectProvenanceRead']} */
const provenance = { state: 'Recorded', source: { id: ids.source, name: 'Synthetic source', version: 3 }, newer: { id: ids.latest, version: 5, status: 'Published' },
  currentPublished: { id: ids.latest, version: 5, status: 'Published' }, asOf: '2026-11-02T12:00:00Z' }
const project = projectDetail({ id: ids.project, projectNumber: '250101', name: 'Synthetic created project 0', createdFromTemplateId: ids.source, templateVersion: 3, templateName: 'Synthetic source' })
const noSourceProject = projectDetail({ id: ids.noSourceProject, projectNumber: 'P-NO-SOURCE', name: 'Synthetic no-source project', createdFromTemplateId: null, templateVersion: null, templateName: null })
const unavailableProject = projectDetail({ id: ids.unavailableProject, projectNumber: 'P-SYN-20', name: 'Synthetic created project 20', createdFromTemplateId: ids.source, templateVersion: null, templateName: null })
const projectReads = [project, noSourceProject, unavailableProject]
const projectRows = projectList(projectReads.map(row => projectRow({ id: row.id, projectNumber: row.projectNumber, name: row.name })))
/** @param {string} projectId @returns {Schemas['NumberingRead']} */
function numberingFor(projectId) {
  const project = projectReads.find(row => row.id === projectId)
  if (!project) throw new Error('Not a finite synthetic project')
  // These projects have no numbering pattern and the synthetic template editor is not their PM.
  return { pattern: null, rowVersion: project.rowVersion, canEdit: false }
}
/** @param {string} projectId @returns {Schemas['TemplateProjectProvenanceRead']} */
function provenanceFor(projectId) {
  if (projectId === ids.project) return structuredClone(provenance)
  if (![ids.noSourceProject, ids.unavailableProject].includes(projectId)) throw new Error('Not a finite synthetic project')
  return { state: projectId === ids.noSourceProject ? 'NotRecorded' : 'Unavailable', source: null, newer: null, currentPublished: null, asOf: provenance.asOf }
}
/** @param {string} selected @param {Schemas['PreviewBody']} body @returns {Schemas['PreviewResult']} */
function preview(selected, body) {
  if (body.milestoneDates && Object.keys(body.milestoneDates).length > 0) throw new Error('This finite fixture has no contractual date override case')
  if (body.disciplineIds?.some(discipline => discipline !== ids.civil)) throw new Error('Only the actual Civil template discipline is available')
  const template = read(selected, true, false)
  const dated = body.startDate ?? null
  return templatePreview({ milestones: template.milestones.map((row, index) => ({ id: row.ref, name: row.name, milestoneType: row.milestoneType, sortOrder: index + 1, date: dated, source: dated ? 'Offset' : 'None' })),
    counts: { milestones: template.milestones.length, deliverables: template.deliverables.length, tasks: template.tasks.length, dependencies: template.dependencies.length },
    undated: { milestones: dated ? 0 : template.milestones.length, deliverables: template.deliverables.length, tasks: template.tasks.length } })
}
module.exports = { ids, templateIds, actorFor, reference, read, list, projects, save, advanceVersion, provenance, provenanceFor, project, projectReads, projectRows, numberingFor, preview }
