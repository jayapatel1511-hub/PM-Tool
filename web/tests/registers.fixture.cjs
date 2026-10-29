// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */

const person = '00000000-0000-0000-0000-000000000002'

/**
 * One risk as the register and the panel read it: open, Medium (2 x 2), owned by Alex Chen, with no review date, mitigation, discipline or issue unless given.
 * @param {Pick<Schemas['RiskListRow'], 'id' | 'projectId' | 'key' | 'title'> & Partial<Schemas['RiskListRow']>} r
 * @returns {Schemas['RiskListRow']}
 */
const riskRow = (r) => ({
  status: 'Open', ownerId: person, ownerName: 'Alex Chen', probability: 2, impact: 2, score: 4, band: 'Medium', mitigation: null, triggerIndicator: null, reviewDate: null,
  isReviewOverdue: false, reviewOverdueDays: 0, projectDisciplineId: null, disciplineName: null, realisedIssueId: null, realisedIssueKey: null, rowVersion: 1, lastActivityAt: '2026-10-01T00:00:00Z', ...r,
})

/**
 * The risk panel's read: the caller may edit and comment, there is nothing to move it to and nothing realised or linked unless given.
 * @param {Pick<Schemas['RiskDetailData'], 'risk'> & Partial<Schemas['RiskDetailData']>} r
 * @returns {Schemas['RiskDetailData']}
 */
const riskDetail = (r) => ({
  description: null, project: { id: r.risk.projectId, projectNumber: 'P-MOCK', name: 'Synthetic live project', status: 'Active' }, realisedIssue: null, links: [],
  permissions: { edit: { ok: true, reason: null }, transitions: [], comment: true }, ...r,
})

/**
 * One issue as the register and the panel read it: an open General issue of Medium severity, owned by Alex Chen, with no dates, locations, references or verification unless given.
 * @param {Pick<Schemas['IssueRow'], 'id' | 'projectId' | 'key' | 'title'> & Partial<Schemas['IssueRow']>} r
 * @returns {Schemas['IssueRow']}
 */
const issueRow = (r) => ({
  status: 'Open', issueType: 'General', raisedById: person, raisedByName: 'Alex Chen', ownerId: person, ownerName: 'Alex Chen', severity: 'Medium', dateRaised: '2026-10-01',
  targetResolutionDate: null, isOverdue: false, daysOverdue: 0, resolution: null, resolvedDate: null, originRiskId: null, originRiskKey: null, projectDisciplineId: null, disciplineName: null,
  locationLabels: [], locationSummary: '', affectedDisciplineIds: [], affectedDisciplineNames: [], affectedDisciplineSummary: '', documentSummary: '', documentIdentifiers: [], documentRevisions: [],
  verificationStatus: 'None', rowVersion: 1, lastActivityAt: '2026-10-01T00:00:00Z', ...r,
})

/**
 * The issue panel's read: the caller may edit, change the type and comment, there is nothing to move it to and no origin risk or links unless given.
 * @param {Pick<Schemas['IssueDetailData'], 'issue'> & Partial<Schemas['IssueDetailData']>} r
 * @returns {Schemas['IssueDetailData']}
 */
const issueDetail = (r) => ({
  description: null, project: { id: r.issue.projectId, projectNumber: 'P-MOCK', name: 'Synthetic live project', status: 'Active' }, originRisk: null, links: [],
  permissions: { edit: { ok: true, reason: null }, changeType: { ok: true, reason: null }, transitions: [], comment: true }, ...r,
})

/**
 * One location of an issue: a site area with nothing else filled in unless given.
 * @param {Pick<Schemas['IssueLocationRow'], 'id'> & Partial<Schemas['IssueLocationRow']>} r
 * @returns {Schemas['IssueLocationRow']}
 */
const locationRow = (r) => ({
  kind: 'SiteArea', siteArea: null, building: null, level: null, room: null, assetSystem: null, alignment: null, startStation: null, endStation: null, stationUnits: null,
  coordinateX: null, coordinateY: null, coordinateZ: null, coordinateReferenceSystem: null, coordinateUnits: null, rowVersion: 1, ...r,
})

/**
 * The answers of the register commands: a raised risk or issue, and the id and version a write leaves behind.
 * @param {string} id
 * @param {string} key
 * @returns {Schemas['RegisterCreated']}
 */
const registerCreated = (id, key) => ({ id, key, rowVersion: 0 })
/** @param {string} id @param {number} rowVersion @returns {Schemas['RegisterSaved']} */
const registerSaved = (id, rowVersion) => ({ id, rowVersion })

module.exports = { riskRow, riskDetail, issueRow, issueDetail, locationRow, registerCreated, registerSaved }
