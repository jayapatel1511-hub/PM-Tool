// @ts-check
/** Synthetic S36 contracts. Production defaults are never seeded by this fixture. */
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */
const { coordinationOptions, coordinationSource, sourceRevisionRow, reviewPage } = require('./reviews.fixture.cjs')
/** @param {number} n */
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
/** @param {Schemas['ProjectDetail']} project @param {string} actor */
function reviewDisciplineFixture(project, actor) {
  const civil = project.disciplines[0].id, geo = id(57101), civilGlobal = id(57102), geoGlobal = id(57103), typeId = id(57104), reviewer = id(57105)
  const sourceA = id(57201), sourceB = id(57202), stamp = '2026-11-02T12:00:00Z'
  /** @type {Schemas['DeliverableTypeRow']} */
  const type = { id: typeId, defaultDisciplineId: civilGlobal, name: 'Drawing Package', isActive: true, sortOrder: 0, auditType: 'ReferenceData',
    auditProjectId: null, auditKey: null, auditName: 'Drawing Package', createdAt: stamp, createdBy: actor, updatedAt: stamp, updatedBy: actor, rowVersion: 7 }
  /** @type {Schemas['DisciplineRow'][]} */
  const disciplines = [
    { id: civilGlobal, name: 'Civil', code: 'CIV', colour: '#64748b', isActive: true, sortOrder: 0, auditType: 'ReferenceData', auditProjectId: null, auditKey: null, auditName: 'Civil', createdAt: stamp, createdBy: actor, updatedAt: stamp, updatedBy: actor, rowVersion: 7 },
    { id: geoGlobal, name: 'Geotechnical', code: 'GEO', colour: '#64748b', isActive: true, sortOrder: 1, auditType: 'ReferenceData', auditProjectId: null, auditKey: null, auditName: 'Geotechnical', createdAt: stamp, createdBy: actor, updatedAt: stamp, updatedBy: actor, rowVersion: 7 },
  ]
  const options = coordinationOptions({ actorId: actor, canWrite: true, canCreate: { changes: false, reviews: true }, manageDisciplineIds: [civil],
    people: [{ id: actor, displayName: 'Synthetic coordinator' }, { id: reviewer, displayName: 'Synthetic independent reviewer' }], disciplines: [{ id: civil, name: 'Civil' }, { id: geo, name: 'Geotechnical' }],
    sources: [sourceA, sourceB].map((source, index) => coordinationSource({ revision: sourceRevisionRow({ id: source, projectId: project.id, deliverableId: id(57301 + index),
      sourceKey: `D-${index + 1}`, title: `Synthetic drawing ${index + 1}`, revision: 'A', sourceIdentity: `deliverable:${id(57301 + index)}`, authorIds: [actor] }) })) })
  /** @type {Schemas['ReviewDisciplineSummary']} */
  const summary = { projectId: project.id, population: 'CurrentApprovedPackages', timingBasis: 'LatestTerminalDisciplineDecision', disciplines: [
    { projectDisciplineId: civil, disciplineId: civilGlobal, name: 'Civil', state: 'Active', approvedPackageCount: 3, eligiblePackageCount: 3, nonApplicablePackageCount: 0,
      packageSampleCount: 3, unavailablePackageCount: 0, medianRoundsToApproval: 2, maximumRoundsToApproval: 3,
      startedAssignmentRoundCount: 6, timedAssignmentRoundCount: 6, unavailableTimingAssignmentRoundCount: 0, medianDaysPerRound: 1.5 },
    { projectDisciplineId: geo, disciplineId: geoGlobal, name: 'Geotechnical', state: 'Active', approvedPackageCount: 3, eligiblePackageCount: 0, nonApplicablePackageCount: 3,
      packageSampleCount: 0, unavailablePackageCount: 0, medianRoundsToApproval: null, maximumRoundsToApproval: null,
      startedAssignmentRoundCount: 0, timedAssignmentRoundCount: 0, unavailableTimingAssignmentRoundCount: 0, medianDaysPerRound: null },
  ] }
  /** @param {string[]} sources @returns {Schemas['ReviewDefaultSuggestions']} */
  const suggestions = sources => ({ projectId: project.id, sourceRevisionIds: [...sources].sort(), exceedsAssignmentLimit: false,
    sourceTypes: sources.map(source => ({ sourceRevisionId: source, deliverableId: source === sourceA ? id(57301) : id(57302), deliverableTypeId: typeId, typeRowVersion: 7, typeActive: true })),
    disciplines: [{ disciplineId: civilGlobal, name: 'Civil', projectDisciplineId: civil, state: 'Available', deliverableTypeIds: [typeId] },
      { disciplineId: geoGlobal, name: 'Geotechnical', projectDisciplineId: geo, state: 'Available', deliverableTypeIds: [typeId] }] })
  /** @type {Schemas['ReviewRegisterPage']} */
  const findings = { items: [], page: 1, pageSize: 50, totalCount: 0, allRounds: false, filters: { disciplineId: null, severity: null, originatorKind: null, originatorId: null, resolverId: null },
    options: { disciplines: [], severities: [], originators: [], resolutionOwners: [] } }
  /** @type {Schemas['ReviewAssignmentGuidance']} */
  const guidance = { allowSelfReview: false, sourcesAvailable: true, authorIds: [actor], excludedReviewerIds: [actor], sources: [] }
  /** @param {number} [rowVersion] @param {string[]} [disciplineIds] @returns {Schemas['ReviewDefaultSettings']} */
  const settings = (rowVersion = 7, disciplineIds = [civilGlobal]) => ({ deliverableTypeId: typeId, rowVersion, disciplineIds: [...disciplineIds] })
  return { civil, geo, civilGlobal, geoGlobal, typeId, reviewer, sourceA, sourceB, type, disciplines, options, summary, suggestions, findings, guidance, settings, packages: reviewPage([]) }
}
module.exports = { reviewDisciplineFixture }
