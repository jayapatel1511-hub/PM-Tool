// @ts-check
/** AC-SCR-71/84 synthetic contracts. The explicit category is test data, not a production D14 seed. */
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */
const { issueRow, issueDetail } = require('./registers.fixture.cjs')
/** @param {number} n */
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
/** @param {Schemas['ProjectDetail']} project @param {string} actor */
function issueRootCauseFixture(project, actor) {
  const category = id(56101), retired = id(56102), target = id(56103)
  /** IssueMetadata reads the current project's options even for a General issue. No extra source/work/person records are needed here.
   * @type {Schemas['CoordinationOptions']} */
  const coordinationOptions = { actorId: actor, canWrite: true, manageDisciplineIds: [], canCreate: { changes: false, reviews: false },
    people: [], disciplines: project.disciplines.filter(discipline => discipline.isActive).map(discipline => ({ id: discipline.id, name: discipline.name })),
    sources: [], heads: [], deliverables: [], tasks: [] }
  /** @type {Schemas['IssueRootCauseChoice'][]} */
  const choices = [{ id: category, name: 'Coordination gap', isActive: true, sortOrder: 0 }, { id: retired, name: 'Retired cause', isActive: false, sortOrder: 1 }]
  /** @returns {Schemas['IssueRow'][]} */
  const fresh = () => [
    ...Array.from({ length: 60 }, (_, index) => issueRow({ id: id(56200 + index), projectId: project.id, key: `ISS-${index + 1}`, title: `Resolved scope issue ${index + 1}`,
      status: 'Resolved', resolution: 'Synthetic recorded resolution', resolvedDate: '2026-11-01', rootCause: { categoryId: category, recordedName: 'Recorded coordination label' },
      locationLabels: ['Location one', 'Location two'], locationSummary: 'Location one; Location two', rowVersion: 3 })),
    ...Array.from({ length: 3 }, (_, index) => issueRow({ id: id(56300 + index), projectId: project.id, key: `OLD-${index + 1}`, title: `Legacy unknown ${index + 1}`, status: 'Resolved', resolution: 'Legacy resolution', resolvedDate: '2026-10-01' })),
    issueRow({ id: target, projectId: project.id, key: 'ISS-OPEN', title: 'Issue awaiting explicit root cause', ownerId: actor, raisedById: actor, rowVersion: 7 }),
  ]
  /** @param {URLSearchParams} query @param {string} key */
  const value = (query, key) => [...query.entries()].find(([name]) => name.toLowerCase() === key.toLowerCase())?.[1]
  /** @param {Schemas['IssueRow'][]} rows @param {URLSearchParams} query */
  const list = (rows, query) => rows.filter(row => {
    const status = value(query, 'status'), cause = value(query, 'rootCause')
    if (status && !status.split(',').includes(row.status)) return false
    if (cause && (row.status !== 'Resolved' || (cause === 'not-recorded' ? row.rootCause != null : row.rootCause?.categoryId !== cause))) return false
    if (value(query, 'location') || value(query, 'document') || value(query, 'verification') || value(query, 'ownerId')) return false
    return true
  })
  /** @param {Schemas['IssueRow'][]} rows @returns {Schemas['IssueRootCauseSummary']} */
  const summary = rows => ({ projectId: project.id, resolvedTotal: rows.filter(row => row.status === 'Resolved').length,
    notRecordedCount: rows.filter(row => row.status === 'Resolved' && !row.rootCause).length,
    causes: [{ categoryId: category, name: 'Coordination gap', isActive: true, count: rows.filter(row => row.status === 'Resolved' && row.rootCause?.categoryId === category).length }] })
  /** @param {Schemas['IssueRow'][]} rows @param {string} issueId @returns {Schemas['IssueDetailData']} */
  const detail = (rows, issueId) => {
    const issue = rows.find(row => row.id === issueId)
    if (!issue) throw new Error('Unknown synthetic issue')
    return issueDetail({ issue, project: { id: project.id, projectNumber: project.projectNumber, name: project.name, status: 'Active' }, permissions: {
      edit: { ok: true, reason: null }, changeType: { ok: true, reason: null }, comment: true,
      transitions: issue.status === 'Resolved' ? [{ to: 'In Progress', ok: true, reason: null }] : [{ to: 'Resolved', ok: true, reason: null }],
    } })
  }
  return { projectId: project.id, category, retired, target, choices, coordinationOptions, fresh, list, summary, detail, value }
}
module.exports = { issueRootCauseFixture }
