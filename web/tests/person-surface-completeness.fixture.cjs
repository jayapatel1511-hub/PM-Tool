// @ts-check
/** Synthetic S43 surfaces; API authority is proved separately by the coordinator's normal-API suite. */
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */
const { entryEdit } = require('./planning.fixture.cjs')
const stamp = '2026-10-31T12:00:00Z'
const week = '2026-10-26'
/** @param {number} n */
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`

/** @param {string} userId @param {string} name @returns {Schemas['PersonPanelRead']} */
const surfacePerson = (userId, name) => ({
  id: userId, displayName: name, isActive: true, jobTitle: 'Synthetic designer', today: '2026-10-31', week, asOf: stamp,
  organisationFacts: { homeDiscipline: null, office: null, supervisor: null, unit: null },
  projects: { items: [], page: 1, pageSize: 25, total: 0 },
})

/**
 * Server's stored mention rows intentionally omit the second UUID-looking token.
 * @param {{author: string, authorName: string, mention: string, mentionName: string, historical: string}} p
 * @returns {Schemas['CommentListResponse']}
 */
const surfaceComments = p => ({ canComment: true, commentReason: null, items: Array.from({ length: 24 }, (_, i) => ({
  id: id(7200 + i), authorId: i === 23 ? p.historical : p.author, authorName: i === 23 ? 'Historical narrow denial' : p.authorName,
  commentKind: 'General', reviewRound: null, createdAt: stamp, editedAt: null, deleted: false, deletedByPm: false, canEdit: false, canDelete: false,
  body: i === 0 ? `Stored @[${p.mentionName}](${p.mention}) and unvalidated @[Unvalidated token](${p.author})` : `Synthetic retained discussion ${i + 1}. Additional context keeps the source panel scrollable.`,
  mentions: i === 0 ? [{ id: p.mention, name: p.mentionName }] : [],
})) })

/** @param {string} actor @param {string} name @param {string} item @returns {Schemas['PageOfActivityEntry']} */
const surfaceHistory = (actor, name, item) => ({ page: 1, pageSize: 100, totalCount: 5, items: [
  entryEdit({ id: id(7301), actorId: actor, actorName: name, itemId: item, occurredAt: stamp }),
  entryEdit({ id: id(7302), actorId: actor, actorName: name, itemId: item, occurredAt: '2026-10-31T11:58:00Z' }),
  entryEdit({ id: id(7303), actorId: actor, actorName: name, actorType: 'Admin', itemId: item, occurredAt: '2026-10-31T11:30:00Z' }),
  entryEdit({ id: id(7304), actorId: actor, actorName: name, actorType: 'Admin', itemId: item, occurredAt: '2026-10-31T11:28:00Z' }),
  entryEdit({ id: id(7305), actorId: null, actorName: null, actorType: 'System', itemId: item, occurredAt: '2026-10-31T11:00:00Z' }),
] })

/** @param {Schemas['AllocationRow']} row @param {Schemas['AllocationShiftPreviewBody']} body @returns {Schemas['AllocationShiftPreview']} */
const surfaceShift = (row, body) => {
  /** @param {string} date */
  const shift = date => { const d = new Date(`${date}T00:00:00Z`); d.setUTCDate(d.getUTCDate() + 7 * body.weeks); return d.toISOString().slice(0, 10) }
  return { mode: body.mode, weeks: body.weeks, asOf: stamp, allocations: [{
    id: row.id, rowVersion: row.rowVersion, personId: row.personId, personName: row.personName ?? 'Unavailable', status: row.status,
    fromDate: row.fromDate, throughDate: row.throughDate, newFromDate: shift(row.fromDate), newThroughDate: shift(row.throughDate),
    requiresConfirmation: row.status === 'Confirmed', unchangedRoles: [],
  }] }
}

/** Two actual discipline references share one User owner; these remain distinct legacy blocker groups.
 * @param {string} projectId @param {string} coordinatorId @param {string} ownerId
 * @returns {{ detail: Schemas['SubmissionDetail'], disciplines: Schemas['CoordinationDiscipline'][] }}
 */
const surfaceBlockerSubmission = (projectId, coordinatorId, ownerId) => {
  const disciplines = [{ id: id(7402), name: 'Synthetic Civil' }, { id: id(7403), name: 'Synthetic Electrical' }]
  return { disciplines, detail: {
    package: { id: id(7400), projectId, key: 'SUB-SHARED-OWNER', title: 'Synthetic separate discipline blockers',
      purpose: 'Synthetic blocker context for the current issue.\n'.repeat(48), recipientReference: 'Synthetic review authority',
      coordinatorId, milestoneId: id(7401), targetDate: '2026-11-12', status: 'Checking', manifestVersion: 1, rowVersion: 1, seq: 1,
      supersedesPackageId: null, auditType: 'SubmissionPackage', auditProjectId: null, auditKey: null, auditName: null,
      createdAt: stamp, createdBy: coordinatorId, updatedAt: stamp, updatedBy: coordinatorId, deletedAt: null, deletedBy: null },
    readiness: { ready: false, fingerprint: 'synthetic-two-discipline-same-owner', blockers: [
      ...Array.from({ length: 18 }, (_, i) => ({ discipline: disciplines[0], index: i })),
      { discipline: disciplines[1], index: 18 },
    ].map(({ discipline, index }) => ({
      code: 'MissingInput', kind: 'Input', disciplineId: discipline.id, ownerId, sourceId: id(7410 + index), sourcePath: null,
      message: `${discipline.name} input ${index + 1} evidence remains open`,
    })) },
    effectiveStatus: 'Checking', manifest: [], checks: [], evidence: [], issue: null, canAuthorise: false, canCoordinate: false,
  } }
}

module.exports = { id, stamp, week, surfacePerson, surfaceComments, surfaceHistory, surfaceShift, surfaceBlockerSubmission }
