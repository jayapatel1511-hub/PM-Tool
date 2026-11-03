// @ts-check
/** Synthetic R1 read contracts, typed against generated DTOs; no API authorization or persistence claim. */
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */
const { coordinationOptions, reviewRow, reviewPage, reviewPackageRow, reviewRoundRow, reviewFindingRow, reviewDetail } = require('./reviews.fixture.cjs')
const { surfacePerson } = require('./person-surface-completeness.fixture.cjs')
/** @param {number} n */
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const stamp = '2026-11-02T12:00:00Z'
/** @param {Schemas['ProjectDetail']} project @param {string} viewer */
function reviewRegisterFixture(project, viewer) {
  const users = { originA: id(9101), originB: id(9102), resolverA: id(9103), resolverB: id(9104), lastResolver: id(9105), unknown: id(9106), coordinator: id(9107) }
  const names = new Map([[users.originA, 'Review originator A'], [users.originB, 'Review originator B'], [users.resolverA, 'Review resolver A'],
    [users.resolverB, 'Review resolver B'], [users.lastResolver, 'End of scope resolver'], [users.coordinator, 'Review coordinator'], [viewer, 'Synthetic current reviewer']])
  /** @param {string} user @returns {Schemas['ReviewRegisterPerson']} */
  const person = user => ({ id: user, name: names.get(user) ?? null, state: names.has(user) ? 'Active' : 'Unknown' })
  const discA = project.disciplines[0].id, discB = id(9251)
  const packageA = reviewPackageRow({ id: id(9201), projectId: project.id, key: 'RV-A', title: 'Retained review package A', status: 'In Review',
    projectDisciplineId: discA, currentRoundId: id(9211), roundNumber: 2, coordinatorId: users.coordinator, requiredForIssue: false,
    purpose: Array.from({ length: 60 }, (_, i) => `Synthetic retained review context ${i + 1}.`).join('\n') })
  const packageB = reviewPackageRow({ id: id(9202), projectId: project.id, key: 'RV-B', title: 'Retained review package B', status: 'Cancelled',
    projectDisciplineId: discB, currentRoundId: id(9221), roundNumber: 2, coordinatorId: users.coordinator, requiredForIssue: false, purpose: packageA.purpose })
  /** @type {Schemas['ReviewRegisterRow'][]} */
  const current = Array.from({ length: 75 }, (_, i) => {
    const a = i < 45, p = a ? packageA : packageB, resolver = i === 74 ? users.lastResolver : a ? users.resolverA : users.resolverB
    const status = i === 46 ? 'Withdrawn' : i % 3 === 0 ? 'Responded' : 'Open'
    const owner = i === 1 ? person(users.unknown) : person(resolver)
    const nextActor = i === 1 ? null : i === 46 ? person(users.coordinator) : status === 'Responded' ? person(users.originA) : owner
    return { id: id(10000 + i), packageId: p.id, packageKey: p.key, packageTitle: p.title, packageStatus: p.status,
      roundId: p.currentRoundId ?? '', roundNumber: 2, isCurrentRound: true, projectDisciplineId: a ? discA : discB,
      disciplineName: a ? 'Civil' : 'Electrical', disciplineState: a ? 'Active' : 'Inactive',
      source: { id: a ? id(9231) : id(9232), key: a ? 'D-A' : 'D-B', revision: 'B', deliverableId: i === 1 ? null : a ? id(9241) : id(9242), available: i !== 1 },
      text: `Retained finding ${String(i + 1).padStart(2, '0')}`, severity: a ? 'Advisory' : 'Blocking', status,
      response: status === 'Responded' ? 'Synthetic recorded response awaiting independent verification.' : null,
      rowVersion: 4, createdAt: new Date(Date.UTC(2026, 10, 2, 12, 0, i)).toISOString(), carriedFromId: i === 4 ? id(10901) : null,
      pendingWithdrawalAcknowledgement: i === 46,
      originator: { kind: 'User', userId: a ? users.originA : users.originB, externalPartyId: null,
        name: names.get(a ? users.originA : users.originB) ?? null, state: 'Active' },
      resolutionOwner: owner, verifier: person(users.originA), coordinator: person(users.coordinator),
      actionability: { isCurrentRound: true, isPackageLive: a, canAct: false, nextAction: i === 46 ? 'acknowledge_withdrawal' : status === 'Responded' ? 'verify_closure' : 'respond_to_comment',
        nextActor, reason: !a ? 'package_terminal' : i === 1 ? 'person_unavailable' : 'not_your_action' } }
  })
  /** @type {Schemas['ReviewRegisterRow'][]} */
  const history = [{ ...current[4], id: id(10901), roundId: id(9210), roundNumber: 1, isCurrentRound: false, carriedFromId: null, text: 'Retained original carried finding',
    actionability: { ...current[4].actionability, isCurrentRound: false, canAct: false, reason: 'historical_round' } },
  { ...current[46], id: id(10902), roundId: id(9220), roundNumber: 1, isCurrentRound: false, text: 'Retained historical pending acknowledgement',
    actionability: { ...current[46].actionability, isCurrentRound: false, canAct: false, reason: 'historical_round' } }]
  const all = [...current, ...history]
  /** @param {URLSearchParams} params @param {string} key */
  const value = (params, key) => [...params].find(([name]) => name.toLowerCase() === key.toLowerCase())?.[1] ?? ''
  /** @param {URLSearchParams} params @returns {Schemas['ReviewRegisterPage']} */
  const page = params => {
    const allRounds = value(params, 'allRounds') === 'true', scope = allRounds ? all : current
    const disciplineId = value(params, 'disciplineId'), severity = value(params, 'severity'), originatorId = value(params, 'originatorId')
    const originatorKind = value(params, 'originatorKind') || (originatorId ? 'User' : ''), resolverId = value(params, 'resolverId')
    const selected = scope.filter(row => (!disciplineId || row.projectDisciplineId === disciplineId) && (!severity || row.severity === severity)
      && (!originatorKind || row.originator.kind === originatorKind) && (!originatorId || row.originator.userId === originatorId)
      && (!resolverId || row.resolutionOwner.id === resolverId))
      .sort((a, b) => a.packageKey.localeCompare(b.packageKey) || a.roundNumber - b.roundNumber || a.createdAt.localeCompare(b.createdAt) || a.id.localeCompare(b.id))
    const pageNumber = Math.max(1, Number(value(params, 'page')) || 1), pageSize = Math.min(200, Math.max(1, Number(value(params, 'pageSize')) || 50))
    const origins = [...new Map(scope.map(row => [row.originator.userId, row.originator])).values()]
    const resolvers = [...new Map(scope.map(row => [row.resolutionOwner.id, row.resolutionOwner])).values()]
    return { items: selected.slice((pageNumber - 1) * pageSize, pageNumber * pageSize), page: pageNumber, pageSize, totalCount: selected.length, allRounds,
      filters: { disciplineId: disciplineId || null, severity: severity || null, originatorKind: originatorKind || null, originatorId: originatorId || null, resolverId: resolverId || null },
      options: { disciplines: [{ id: discA, name: 'Civil', state: 'Active' }, { id: discB, name: 'Electrical', state: 'Inactive' }],
        severities: ['Advisory', 'Blocking'], originators: origins, resolutionOwners: resolvers } }
  }
  /** @param {Schemas['ReviewRegisterRow']} row @returns {Schemas['RecordContextView']} */
  const context = row => ({ owner: { ...row.resolutionOwner, kind: 'User' }, nextActors: row.actionability.nextActor ? [{ ...row.actionability.nextActor, kind: 'User' }] : [],
    actorState: row.actionability.nextActor?.state ?? 'Unknown', nextAction: row.actionability.nextAction, neededBy: null, inputs: [], nextTransitions: [],
    requiresReviewGate: false, terminal: !row.actionability.isPackageLive })
  /** @param {Schemas['ReviewRegisterRow']} row @param {boolean} patterns @returns {Schemas['ReviewFindingLocation']} */
  const locator = (row, patterns) => ({ id: row.id, packageId: row.packageId, roundId: row.roundId, key: row.packageKey, title: row.text,
    ...(patterns ? { context: context(row) } : {}) })
  /** @param {string} packageId @returns {Schemas['ReviewDetail']} */
  const detail = packageId => {
    const p = packageId === packageA.id ? packageA : packageB, rows = all.filter(row => row.packageId === packageId)
    return reviewDetail({ package: p, actorId: viewer, canWrite: false, rounds: [reviewRoundRow({ id: packageId === packageA.id ? id(9210) : id(9220), packageId, projectId: project.id, number: 1, status: 'Changes Required' }),
      reviewRoundRow({ id: p.currentRoundId ?? '', packageId, projectId: project.id, number: 2, status: p.status })],
      findings: rows.map(row => reviewFindingRow({ id: row.id, packageId, projectId: project.id, roundId: row.roundId, sourceRevisionId: row.source.id,
        projectDisciplineId: row.projectDisciplineId, originatorId: row.originator.userId ?? '', resolverId: row.resolutionOwner.id ?? '', verifierId: users.originA,
        text: row.text, severity: row.severity, status: row.status, response: row.response, carriedFromId: row.carriedFromId,
        withdrawalAcknowledgedBy: null, rowVersion: row.rowVersion })) })
  }
  const options = coordinationOptions({ actorId: viewer, canWrite: false, people: [...names].map(([userId, displayName]) => ({ id: userId, displayName })),
    disciplines: [{ id: discA, name: 'Civil' }, { id: discB, name: 'Electrical' }] })
  // CurrentApprovedPackages has no samples here: A is In Review, B is Cancelled. Findings do not imply approval or timing.
  const noApprovedSamples = { approvedPackageCount: 0, eligiblePackageCount: 0, nonApplicablePackageCount: 0, packageSampleCount: 0,
    unavailablePackageCount: 0, medianRoundsToApproval: null, maximumRoundsToApproval: null,
    startedAssignmentRoundCount: 0, timedAssignmentRoundCount: 0, unavailableTimingAssignmentRoundCount: 0, medianDaysPerRound: null }
  /** @type {Schemas['ReviewDisciplineSummary']} */
  const summary = { projectId: project.id, population: 'CurrentApprovedPackages', timingBasis: 'LatestTerminalDisciplineDecision', disciplines: [
    { ...noApprovedSamples, projectDisciplineId: discA, disciplineId: project.disciplines[0].disciplineId, name: 'Civil', state: 'Active' },
    { ...noApprovedSamples, projectDisciplineId: discB, disciplineId: id(9252), name: 'Electrical', state: 'Inactive' },
  ] }
  /** @param {number} pageNumber */
  const packages = pageNumber => reviewPage([pageNumber === 1 ? reviewRow({ id: packageA.id, key: packageA.key, title: packageA.title, status: packageA.status })
    : reviewRow({ id: packageB.id, key: packageB.key, title: packageB.title, status: packageB.status })], { page: pageNumber, pageSize: 1, total: 2 })
  /** @param {string} user @param {string} name @returns {Schemas['PersonPanelRead']} */
  const panelPerson = (user, name) => surfacePerson(user, name)
  return { users, names, discA, discB, packageA, packageB, current, history, all, page, locator, detail, options, summary, packages, value, person: panelPerson, stamp }
}
module.exports = { reviewRegisterFixture, id, stamp }
