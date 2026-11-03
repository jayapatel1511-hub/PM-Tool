// @ts-check
/** Synthetic AC-SCR-61/84 browser contracts, not evidence of actual API authorization or persistence. */
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */
const { reviewRegisterFixture, id, stamp } = require('./review-register.fixture.cjs')
const { reviewFindingRow, reviewFindingEventRow, reviewDetail } = require('./reviews.fixture.cjs')
const outcomes = ['Incorporated', 'Not incorporated', 'Clarified', 'Noted']
/** @param {Schemas['ProjectDetail']} project @param {string} viewer */
function reviewResponseFixture(project, viewer) {
  const r1 = reviewRegisterFixture(project, viewer), p = r1.packageA
  const source = r1.current[0].source.id, verifier = r1.users.originA
  /** @returns {Schemas['ReviewDetail']} */
  const fresh = () => reviewDetail({ package: p, actorId: viewer, canWrite: true,
    rounds: r1.detail(p.id).rounds,
    findings: [
      ...outcomes.map((outcome, index) => reviewFindingRow({ id: id(15101 + index), projectId: project.id, packageId: p.id,
        roundId: p.currentRoundId ?? '', sourceRevisionId: source, projectDisciplineId: r1.discA, originatorId: verifier,
        resolverId: viewer, verifierId: verifier, text: `Response fixture ${index + 1}: ${outcome}`, severity: 'Advisory', rowVersion: 7 })),
      reviewFindingRow({ id: id(15110), projectId: project.id, packageId: p.id, roundId: id(9210), sourceRevisionId: source,
        projectDisciplineId: r1.discA, originatorId: verifier, resolverId: viewer, verifierId: verifier,
        text: 'Earlier round comment retained after carry', status: 'Verified Closed', response: 'Legacy response without a recorded outcome.',
        evidenceUrl: 'https://example.test/legacy-evidence', rowVersion: 3 }),
      reviewFindingRow({ id: id(15111), projectId: project.id, packageId: p.id, roundId: p.currentRoundId ?? '', sourceRevisionId: source,
        projectDisciplineId: r1.discA, originatorId: verifier, resolverId: viewer, verifierId: verifier,
        text: 'Unknown imported outcome stays unknown', status: 'Responded', response: 'Imported response retained without inference.',
        responseOutcome: 'Unknown legacy outcome', evidenceUrl: 'https://example.test/unknown-evidence', rowVersion: 3 }),
    ],
    events: [reviewFindingEventRow({ id: id(15201), projectId: project.id, findingId: id(15110), action: 'Responded',
      reason: 'Legacy response without a recorded outcome.', createdBy: verifier })],
  })
  /** @param {Schemas['ReviewDetail']} data @param {URLSearchParams} query @returns {Schemas['ReviewRegisterPage']} */
  const register = (data, query) => {
    const severity = r1.value(query, 'severity')
    const selected = data.findings.filter(row => ['Open', 'Responded'].includes(row.status) && (!severity || row.severity === severity))
    const items = selected.map(row => ({ ...r1.current[0], id: row.id, packageId: p.id, packageKey: p.key, packageTitle: p.title,
      roundId: row.roundId, roundNumber: 2, text: row.text, status: row.status, severity: row.severity,
      response: row.response, responseOutcome: row.responseOutcome, rowVersion: row.rowVersion,
      resolutionOwner: { id: viewer, name: 'Synthetic current reviewer', state: 'Active' },
      actionability: { ...r1.current[0].actionability, canAct: row.status === 'Open', reason: row.status === 'Open' ? null : 'not_your_action',
        nextAction: row.status === 'Responded' ? 'verify_closure' : 'respond_to_comment',
        nextActor: { id: row.status === 'Responded' ? verifier : viewer, name: row.status === 'Responded' ? 'Review originator A' : 'Synthetic current reviewer', state: 'Active' } } }))
    return { ...r1.page(query), items, totalCount: items.length, page: 1, pageSize: 50 }
  }
  /** @param {Schemas['ReviewFindingRow']} finding @param {boolean} patterns @returns {Schemas['ReviewFindingLocation']} */
  const locator = (finding, patterns) => ({ id: finding.id, packageId: p.id, roundId: finding.roundId, key: p.key, title: finding.text,
    ...(patterns ? { context: r1.locator({ ...r1.current[0], id: finding.id }, true).context } : {}) })
  /** @param {Schemas['ReviewDetail']} data @param {string} findingId @param {Schemas['FindingAction']} body */
  const apply = (data, findingId, body) => {
    const finding = data.findings.find(row => row.id === findingId)
    if (!finding) throw new Error('Synthetic response target missing')
    finding.response = body.reason; finding.evidenceUrl = body.evidenceUrl; finding.responseOutcome = body.responseOutcome
    finding.status = 'Responded'; finding.rowVersion += 1
    data.events.push(reviewFindingEventRow({ id: id(15300 + data.events.length), projectId: project.id, findingId, action: 'Responded',
      reason: body.reason, evidenceUrl: body.evidenceUrl, responseOutcome: body.responseOutcome, createdBy: viewer, createdAt: stamp }))
    return { id: finding.id, rowVersion: finding.rowVersion }
  }
  return { fresh, register, locator, apply, package: p, options: r1.options, summary: r1.summary, packages: r1.packages, outcomes, id, stamp }
}

module.exports = { reviewResponseFixture, outcomes }
