// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */

/**
 * One allocation as the list shows it: a proposed five-day production request for a person who is on the project, unless given.
 * @param {Pick<Schemas['AllocationRow'], 'id' | 'personId'> & Partial<Schemas['AllocationRow']>} r
 * @returns {Schemas['AllocationRow']}
 */
const allocationRow = (r) => ({ personName: 'Jill Partial', purpose: 'Production', fromDate: '2026-10-05', throughDate: '2026-10-09', plannedHours: 16, status: 'Proposed', rowVersion: 0, personState: 'Eligible', ...r })

/**
 * The allocations list answer: one page of the rows given, with no sort or filter echoed back.
 * @param {Schemas['AllocationRow'][]} items
 * @param {Partial<Pick<Schemas['PageOfAllocationRow'], 'page' | 'pageSize' | 'totalCount'>>} [r]
 * @returns {Schemas['PageOfAllocationRow']}
 */
const allocationPage = (items, r = {}) => ({ items, page: 1, pageSize: 50, totalCount: items.length, ...r })

/**
 * One allocation's detail: the row plus who may act on it, with no days, links or confirmation unless given.
 * @param {Schemas['AllocationRow'] & Partial<Schemas['AllocationDetail']>} r
 * @returns {Schemas['AllocationDetail']}
 */
const allocationDetail = (r) => ({ projectId: '00000000-0000-0000-0000-000000000010', personEligible: true, confirmedBy: null, confirmedAt: null, overCapacityWarningRecorded: false, canConfirm: false, canManage: false, days: [], links: [], ...r })

/**
 * A task link on an allocation: no review hours and no review package unless given.
 * @param {Pick<Schemas['AllocationLink'], 'workId' | 'workDate'> & Partial<Schemas['AllocationLink']>} r
 * @returns {Schemas['AllocationLink']}
 */
const allocationLink = (r) => ({ workType: 'Task', reviewHours: null, reviewPackageId: null, ...r })

/**
 * One day of the confirmation preview: the person's available hours with this allocation's hours proposed, unless given.
 * @param {Pick<Schemas['CapacityDay'], 'date'> & Partial<Schemas['CapacityDay']>} r
 * @returns {Schemas['CapacityDay']}
 */
const capacityDay = (r) => ({ availableHours: 8, confirmedHours: 0, proposedHours: 6, resultingHours: 6, overByHours: 0, dateVersion: 1, ...r })

/**
 * The confirmation preview answer for an allocation, on the row version the confirmation must name.
 * @param {Pick<Schemas['ConfirmationPreviewRead'], 'id' | 'days'> & Partial<Schemas['ConfirmationPreviewRead']>} r
 * @returns {Schemas['ConfirmationPreviewRead']}
 */
const confirmationPreview = (r) => ({ rowVersion: 1, ...r })

/**
 * A person's availability override on one day, as the Workload day dialog reads it: reduced to four hours unless given.
 * @param {Pick<Schemas['DayAvailabilityRow'], 'workDate' | 'rowVersion'> & Partial<Schemas['DayAvailabilityRow']>} r
 * @returns {Schemas['DayAvailabilityRow']}
 */
const dayAvailability = (r) => ({ availableHours: 4, category: 'Reduced', ...r })

module.exports = { allocationRow, allocationPage, allocationDetail, allocationLink, capacityDay, confirmationPreview, dayAvailability }
