// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']['DecisionRow']} DecisionRow */
/** @typedef {import('../src/gen/api').components['schemas']['DecisionDetailResponse']} DecisionDetailResponse */
/** @typedef {import('../src/gen/api').components['schemas']['ExternalPartyRow']} ExternalPartyRow */

const ids = {
  project: '00000000-0000-0000-0000-000000000010',
  requester: '00000000-0000-0000-0000-000000000002',
  decision: '00000000-0000-0000-0000-000000000301',
  party: '00000000-0000-0000-0000-000000000302',
}

/** @type {ExternalPartyRow} */
const externalParty = {
  id: ids.party, name: 'Synthetic client', organisation: 'Synthetic organisation', email: 'client@example.test',
  role: 'Client representative', isClient: true, notes: null, isActive: true, rowVersion: 1,
}

/** @type {DecisionRow} */
const decisionRow = {
  id: ids.decision, projectId: ids.project, key: 'DEC-001', subject: 'Synthetic decision', status: 'Pending',
  requestedById: ids.requester, requestedByName: 'Alex Chen', ownerUserId: null, ownerExternalPartyId: ids.party,
  ownerName: externalParty.name, ownerOrganisation: externalParty.organisation, ownerIsClient: true,
  dateRequested: '2026-10-03', requiredByDate: '2026-10-10', originalRequiredByDate: '2026-10-10',
  impactLevel: 'Medium', impactDescription: 'Synthetic impact', decisionText: null, decisionDate: null,
  rowVersion: 1, daysUntil: 7, isOverdue: false, daysOverdue: 0, isDueSoon: false, isInactiveOwner: false,
  blockingTaskIds: [], deliverables: [], milestones: [],
}

/** @type {DecisionDetailResponse} */
const decisionDetail = {
  decision: decisionRow, description: 'Synthetic decision detail', deferralReason: null, cancelledReason: null, decidedBy: null,
  project: { id: ids.project, projectNumber: 'P-MOCK', name: 'Synthetic live project', status: 'Active' },
  links: [], permissions: { edit: { ok: true, reason: null }, transitions: [], comment: true },
}

module.exports = { ids, externalParty, decisionRow, decisionDetail }
