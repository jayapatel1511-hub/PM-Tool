// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */

/**
 * One version in the template list: a Draft with no structure and no project unless given. `roles` is left out, as the server leaves it out
 * while flag.template_roles is off.
 * @param {Pick<Schemas['TemplateListRow'], 'id' | 'name'> & Partial<Schemas['TemplateListRow']>} r
 * @returns {Schemas['TemplateListRow']}
 */
const templateRow = (r) => ({
  familyId: r.id, description: null, projectTypeId: null, version: null, status: 'Draft', publishedAt: null, updatedAt: '2026-10-01T12:00:00+00:00', rowVersion: 1,
  disciplines: 0, milestones: 0, deliverables: 0, tasks: 0, projects: 0, ...r,
})

/**
 * GET /templates: editors see every version, everyone else the published ones.
 * @param {Schemas['TemplateListRow'][]} templates
 * @param {boolean} [canEdit]
 * @returns {Schemas['TemplateList']}
 */
const templateList = (templates, canEdit = true) => ({ canEdit, templates })

/**
 * One template as the editor reads and saves it: an empty Draft the caller may edit. `roles` and `offsetDayKind` are left out, as the server leaves
 * them out while their flags are off.
 * @param {Pick<Schemas['TemplateRead'], 'id' | 'name'> & Partial<Schemas['TemplateRead']>} r
 * @returns {Schemas['TemplateRead']}
 */
const templateRead = (r) => ({
  familyId: r.id, description: null, projectTypeId: null, version: null, status: 'Draft', publishedAt: null, rowVersion: 1, canEdit: true,
  family: [], disciplines: [], basisSuggestions: [], milestones: [], deliverables: [], tasks: [], dependencies: [], ...r,
})

/**
 * The preview: what a project made from the template would get, with every count zero and nothing undated unless given. `roles` is left out
 * while flag.template_roles is off.
 * @param {Partial<Schemas['PreviewResult']>} [r]
 * @returns {Schemas['PreviewResult']}
 */
const templatePreview = (r = {}) => ({
  milestones: [], counts: { milestones: 0, deliverables: 0, tasks: 0, dependencies: 0 }, undated: { milestones: 0, deliverables: 0, tasks: 0 }, ...r,
})

/**
 * One dated milestone in the preview.
 * @param {Pick<Schemas['PreviewMilestone'], 'id' | 'name'> & Partial<Schemas['PreviewMilestone']>} m
 * @returns {Schemas['PreviewMilestone']}
 */
const previewMilestone = (m) => ({ milestoneType: 'Kickoff', sortOrder: 1, date: null, source: 'Offset', ...m })

/** @param {string} id @param {number} [rowVersion] @returns {Schemas['TemplateCreated']} */
const templateCreated = (id, rowVersion = 0) => ({ id, rowVersion })
/** The family's Draft, started now (201) or already there (200). @param {string} id @param {number} [rowVersion] @returns {Schemas['TemplateDraft']} */
const templateDraft = (id, rowVersion = 0) => ({ id, rowVersion })
/** @param {string} id @param {number} version @param {number} rowVersion @returns {Schemas['TemplatePublished']} */
const templatePublished = (id, version, rowVersion) => ({ id, version, status: 'Published', rowVersion })

/**
 * The proposal for adding a template discipline to a project: the project's milestones and, per discipline, what it adds and how its milestones map.
 * @param {Pick<Schemas['PackProposalRead'], 'templateId' | 'name'> & Partial<Schemas['PackProposalRead']>} r
 * @returns {Schemas['PackProposalRead']}
 */
const packProposal = (r) => ({ version: 1, projectMilestones: [], disciplines: [], ...r })

/** @param {number} deliverables @param {number} tasks @param {number} dependencies @returns {Schemas['PackAdded']} */
const packAdded = (deliverables, tasks, dependencies) => ({ deliverables, tasks, dependencies })

module.exports = { templateRow, templateList, templateRead, templatePreview, previewMilestone, templateCreated, templateDraft, templatePublished, packProposal, packAdded }
