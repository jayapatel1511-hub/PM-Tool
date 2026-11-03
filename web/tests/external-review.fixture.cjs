// @ts-check
/** Synthetic UI contracts. Provider authorization, persistence and issue capture are tested separately. */
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */
const { id, stamp } = require('./review-register.fixture.cjs')
const { coordinationOptions, coordinationSource, sourceRevisionRow, reviewPackageRow, reviewRoundRow,
  reviewAssignmentRow, reviewFindingRow, reviewFindingEventRow, reviewManifestRow, reviewDetail, reviewRow, reviewPage } = require('./reviews.fixture.cjs')

/** @param {Schemas['ProjectDetail']} project @param {string} viewer */
function externalReviewFixture(project, viewer) {
  const discipline = project.disciplines[0].id, author = id(18101), resolver = id(18102), partyId = id(18103)
  const packageId = id(18201), originalRound = id(18202), currentRound = id(18203), deliverableId = id(18301)
  /** @type {Schemas['ReviewIssuedSourceRead']} */
  const original = { deliverableIssueId: id(18311), deliverableId, deliverableKey: 'D-EXT-01', revision: 'A', issuedDate: '2026-10-20', rowVersion: 4, authorsState: 'Captured', selectable: true, reason: null }
  /** @type {Schemas['ReviewIssuedSourceRead']} */
  const current = { ...original, deliverableIssueId: id(18312), revision: null, issuedDate: '2026-10-26', rowVersion: 8 }
  /** @type {Schemas['ReviewIssuedSourceRead']} */
  const unknown = { ...original, deliverableIssueId: id(18313), deliverableId: id(18302), deliverableKey: 'D-UNKNOWN', revision: 'X', authorsState: 'Unknown', selectable: false, reason: 'Captured authors are unknown; this revision cannot be selected' }
  /** @type {Schemas['ExternalReviewPartyRead']} */
  const party = { id: partyId, rowVersion: 5, name: 'Synthetic external reviewer', organisation: 'Synthetic review office' }
  /** @type {Schemas['ExternalReviewCodeRead']} */
  const code = { id: id(18401), rowVersion: 6, code: 'B', name: 'Revise and return' }
  const p = reviewPackageRow({ id: packageId, projectId: project.id, projectDisciplineId: discipline, coordinatorId: viewer,
    key: 'RV-EXT', title: 'Synthetic issued review', purpose: 'Review the received comments against the actual issued revision',
    status: 'Approved', currentRoundId: currentRound, roundNumber: 2, requiredForIssue: false, rowVersion: 11 })
  const internalSource = sourceRevisionRow({ id: id(18501), projectId: project.id, deliverableId, sourceKey: 'SRC-INTERNAL', title: 'Synthetic internal reference', revision: 'I' })
  const options = coordinationOptions({ actorId: viewer, canWrite: true, canCreate: { changes: false, reviews: true }, manageDisciplineIds: [discipline],
    people: [{ id: viewer, displayName: 'Synthetic PM verifier' }, { id: author, displayName: 'Captured issue author' }, { id: resolver, displayName: 'Synthetic resolver' }],
    disciplines: [{ id: discipline, name: 'Civil' }], sources: [coordinationSource({ revision: internalSource })] })
  /** @type {Schemas['ExternalReviewRead']} */
  const priorReceipt = { id: id(18601), rowVersion: 1, packageId, roundId: originalRound, deliverableIssueId: original.deliverableIssueId,
    externalPartyId: party.id, partyName: party.name, partyOrganisation: party.organisation, partyState: 'Active', receivedDate: '2026-10-22', returnCodeId: code.id,
    returnCode: code.code, returnLabel: code.name, recordedBy: viewer, recordedAt: stamp, commentCount: 1 }
  /** @returns {Schemas['ReviewDetail']} */
  const fresh = () => reviewDetail({ package: { ...p }, actorId: viewer, canWrite: true, canCoordinate: true, canManage: true, canReassignVerifier: true,
    eligiblePeople: [viewer, author, resolver], canRecordExternalReview: true,
    rounds: [reviewRoundRow({ id: originalRound, packageId, projectId: project.id, number: 1, status: 'Changes Required', startedAt: stamp }),
      reviewRoundRow({ id: currentRound, packageId, projectId: project.id, number: 2, status: 'Approved', startedAt: stamp })],
    assignments: [reviewAssignmentRow({ id: id(18204), roundId: currentRound, projectId: project.id, projectDisciplineId: discipline, reviewerId: viewer, status: 'Approved' })],
    manifest: [reviewManifestRow({ id: id(18205), roundId: originalRound, deliverableId, sourceRevisionId: null, source: null, authorIds: [author], issuedSource: { ...original } }),
      reviewManifestRow({ id: id(18206), roundId: currentRound, deliverableId, sourceRevisionId: null, source: null, authorIds: [author], issuedSource: { ...current } })],
    externalReviews: [{ ...priorReceipt }],
    findings: [reviewFindingRow({ id: id(18701), packageId, projectId: project.id, roundId: currentRound, sourceRevisionId: null, deliverableIssueId: current.deliverableIssueId,
      externalReviewId: priorReceipt.id, originatorId: null, originator: { kind: 'ExternalParty', userId: null, externalPartyId: party.id, name: party.name, state: 'Active' },
      projectDisciplineId: discipline, resolverId: resolver, verifierId: null, text: 'Synthetic carried external comment', severity: 'Advisory', status: 'Responded',
      response: 'Synthetic resolver response awaiting independent review', responseOutcome: 'Clarified', evidenceUrl: 'https://example.test/resolver-evidence', carriedFromId: id(18700), rowVersion: 3 })],
  })
  /** @returns {Schemas['ExternalReviewOptions']} */
  const recordOptions = () => ({ issuedRevisions: [{ ...current }], parties: [{ ...party }], returnCodes: [{ ...code }], canRecord: true })
  /** @param {string[]} ids @returns {Schemas['ReviewIssuedAssignmentGuidance']} */
  const guidance = ids => ({ allowSelfReview: false, sourcesAvailable: ids.length > 0 && ids.every(value => [original.deliverableIssueId, current.deliverableIssueId].includes(value)),
    authorIds: [author], excludedReviewerIds: [author], sources: [original, current, unknown].filter(row => ids.includes(row.deliverableIssueId)) })
  /** @param {URLSearchParams} query */
  const sources = query => {
    const all = [current, unknown, original].filter(row => !query.get('q') || row.deliverableKey.includes(query.get('q') ?? ''))
    return { items: all, page: 1, pageSize: 50, totalCount: all.length }
  }
  /** @param {Schemas['ReviewDetail']} data */
  const packages = data => reviewPage([reviewRow({ id: p.id, key: p.key, title: p.title, status: data.package.status, rowVersion: data.package.rowVersion })])
  /** @param {Schemas['ReviewDetail']} data @param {Schemas['ExternalReviewRecordBody']} body */
  const record = (data, body) => {
    const receipts = data.externalReviews ?? (data.externalReviews = [])
    const receiptId = id(18610 + receipts.length)
    receipts.push({ ...priorReceipt, id: receiptId, roundId: currentRound, deliverableIssueId: body.deliverableIssueId,
      externalPartyId: body.externalPartyId, receivedDate: body.receivedDate, returnCodeId: body.returnCodeId, commentCount: body.comments.length })
    for (const comment of body.comments) data.findings.push(reviewFindingRow({ id: id(18710 + data.findings.length), packageId, projectId: project.id, roundId: currentRound,
      sourceRevisionId: null, deliverableIssueId: body.deliverableIssueId, externalReviewId: receiptId, originatorId: null, originator: { kind: 'ExternalParty', userId: null, externalPartyId: party.id, name: party.name, state: 'Active' },
      projectDisciplineId: comment.projectDisciplineId, resolverId: comment.resolverId, verifierId: null, text: comment.text, severity: comment.severity, issueId: comment.issueId ?? null }))
    data.package.rowVersion += 1; data.package.status = 'Changes Required'
    return { id: receiptId, rowVersion: 1 }
  }
  /** @param {Schemas['ReviewDetail']} data @param {string} findingId @param {Schemas['FindingAction']} body */
  const action = (data, findingId, body) => {
    const finding = data.findings.find(row => row.id === findingId)
    if (!finding) throw new Error('Synthetic finding missing')
    if (body.action === 'assignVerifier') finding.verifierId = body.ownerId
    if (body.action === 'Verified Closed') finding.status = 'Verified Closed'
    finding.rowVersion += 1
    data.events.push(reviewFindingEventRow({ id: id(18800 + data.events.length), projectId: project.id, findingId, action: body.action,
      reason: body.reason, evidenceUrl: body.evidenceUrl ?? null, createdBy: viewer, createdAt: stamp }))
    return { id: finding.id, rowVersion: finding.rowVersion }
  }
  /** @returns {Schemas['ReviewRegisterPage']} */
  const register = () => ({ items: [], page: 1, pageSize: 50, totalCount: 0, allRounds: false,
    filters: { disciplineId: null, severity: null, originatorKind: null, originatorId: null, resolverId: null },
    options: { disciplines: [], severities: [], originators: [], resolutionOwners: [] } })
  return { id, stamp, p, viewer, author, resolver, discipline, original, current, unknown, party, code, options, internalSource,
    fresh, recordOptions, guidance, sources, packages, record, action, register }
}
module.exports = { externalReviewFixture }
