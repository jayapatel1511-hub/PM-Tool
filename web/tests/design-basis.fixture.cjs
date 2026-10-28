// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */

const none = '00000000-0000-4000-8000-000000000000'
const stamp = '2026-10-01T09:00:00Z'

/**
 * One row of the design basis register, typed against the contract so a server rename of a field fails `npm run typecheck:tests`.
 * @param {Pick<Schemas['BasisRegisterRow'], 'id' | 'key' | 'title'> & Partial<Schemas['BasisRegisterRow']>} r
 * @returns {Schemas['BasisRegisterRow']}
 */
const basisRegisterRow = (r) => ({
  kind: 'Criterion', ownerId: none, projectDisciplineId: none, currentVersionId: null, rowVersion: 2, currentStatus: null, currentScope: null,
  latestStatus: null, latestVersionId: null, confirmationDueDate: null, conflictCount: 0, ...r,
})

/**
 * One page of the register. The page and size are the request's, as the server echoes them.
 * @param {Schemas['BasisRegisterRow'][]} items
 * @param {{ total?: number, page?: number, pageSize?: number }} [paging]
 * @returns {Schemas['PageOfBasisRegisterRow']}
 */
const basisPage = (items, paging = {}) => ({ items, page: paging.page ?? 1, pageSize: paging.pageSize ?? 50, totalCount: paging.total ?? items.length })

/**
 * The stored entry, audit fields included.
 * @param {Pick<Schemas['BasisEntryRow'], 'id' | 'key' | 'title'> & Partial<Schemas['BasisEntryRow']>} r
 * @returns {Schemas['BasisEntryRow']}
 */
const basisEntryRow = (r) => ({
  kind: 'Criterion', ownerId: none, projectDisciplineId: none, independentApproverId: null, currentVersionId: null, auditType: 'DesignBasisEntry', auditProjectId: null,
  auditKey: r.key, auditName: r.title, auditDisciplineId: null, projectId: none, seq: 1, deletedAt: null, deletedBy: null, createdAt: stamp, createdBy: null,
  updatedAt: stamp, updatedBy: null, rowVersion: 2, ...r,
})

/**
 * One stored version, with the flag the register reads for an incomplete source.
 * @param {Pick<Schemas['BasisVersionRow'], 'id' | 'entryId' | 'number' | 'status'> & Partial<Schemas['BasisVersionRow']>} r
 * @param {boolean} [sourceMissing]
 * @returns {Schemas['BasisVersionItem']}
 */
const basisVersionItem = (r, sourceMissing = false) => ({
  version: {
    supersedesVersionId: null, scope: 'Site - watermain', statement: 'Minimum cover', numericValue: 1.5, units: 'm', sourceSystem: 'DMS', stableSourceId: null,
    sourceUrl: 'https://example.test/a', declaredRevision: 'A', confirmationDueDate: null, decisionId: null, confirmedBy: null, confirmedAt: null, confirmationRationale: null,
    auditType: 'DesignBasisVersion', auditName: 'Minimum cover', projectId: none, auditProjectId: null, auditKey: null, createdAt: stamp, createdBy: null, updatedAt: stamp,
    updatedBy: null, rowVersion: 1, ...r,
  },
  sourceMissing,
})

/**
 * @param {Pick<Schemas['BasisUseRow'], 'id' | 'versionId' | 'targetId'> & Partial<Schemas['BasisUseRow']>} r
 * @returns {Schemas['BasisUseRow']}
 */
const basisUseRow = (r) => ({ targetType: 'Task', ownerId: none, intendedUse: 'Cover check', rowVersion: 1, isCurrent: true, ...r })

/**
 * @param {Pick<Schemas['BasisImpactRow'], 'id' | 'basisUseId' | 'oldVersionId'> & Partial<Schemas['BasisImpactRow']>} r
 * @returns {Schemas['BasisImpactRow']}
 */
const basisImpactRow = (r) => ({
  newVersionId: null, withdrawalVersionId: null, ownerId: none, status: 'Pending Assessment', rationale: null, evidenceUrl: null, decidedBy: null, decidedAt: null,
  auditType: 'BasisImpactAssessment', projectId: none, auditProjectId: null, auditKey: null, auditName: null, createdAt: stamp, createdBy: null, updatedAt: stamp,
  updatedBy: null, rowVersion: 1, ...r,
})

/**
 * One entry as the detail panel reads it.
 * @param {Pick<Schemas['BasisDetail'], 'entry' | 'versions'> & Partial<Schemas['BasisDetail']>} r
 * @returns {Schemas['BasisDetail']}
 */
const basisDetail = (r) => ({ uses: [], impacts: [], conflicts: [], dispositions: [], canManage: false, canEditProposed: false, canConfirm: false, ...r })

module.exports = { basisRegisterRow, basisPage, basisEntryRow, basisVersionItem, basisUseRow, basisImpactRow, basisDetail }
