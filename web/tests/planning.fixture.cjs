// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */

/**
 * A plan entry a supervisor made for a person: a draft with no project, editable, unflagged and not in any filter, unless given.
 * @param {Pick<Schemas['EntryRow'], 'id' | 'personId' | 'personName' | 'ownerId' | 'ownerName' | 'label' | 'startWeek' | 'endWeek'> & Partial<Schemas['EntryRow']>} r
 * @returns {Schemas['EntryRow']}
 */
const planningEntry = (r) => ({
  ownerKind: 'Manager', sourceCategory: 'Other', projectId: null, projectNumber: null, projectName: null, projectDisciplineId: null, disciplineName: null,
  hoursPerWeek: 8, confidence: 'Expected', visibility: 'Draft', notes: null, lastValidatedAt: '2026-10-01T00:00:00Z', stale: false, warnings: [],
  canEdit: true, canChangeVisibility: true, correctionOnly: false, rowVersion: 1, matchesFilter: true, weekHours: null, countedHours: null, coveredHours: null, ...r,
})

/**
 * One person-week with nothing planned and the contracted 40 hours free, unless given.
 * @param {Pick<Schemas['CellRow'], 'week'> & Partial<Schemas['CellRow']>} r
 * @returns {Schemas['CellRow']}
 */
const planningCell = (r) => ({
  capacity: 40, timeAway: 0, additionalAvailability: 0, approved: 0, confirmed: 0, expected: 0, possible: 0, remaining: 40, taskEstimates: 0, unestimatedTasks: 0,
  overPlanned: false, underPlanned: false, staleEntries: 0, ownDraftHours: 0, holidayHours: 0, provisional: false, ...r,
})

/**
 * A person in the grid the viewer cannot plan for, with no cells, entries or allocations, unless given.
 * @param {Pick<Schemas['PlanningPersonRow'], 'id' | 'displayName'> & Partial<Schemas['PlanningPersonRow']>} r
 * @returns {Schemas['PlanningPersonRow']}
 */
const planningPerson = (r) => ({
  supervisorId: null, supervisorName: null, weeklyCapacity: 40, canCreateSelfEntry: false, canCreateManagerEntry: false, canRecordTimeAway: false,
  indicators: [], cells: [], entries: [], approvedAllocations: [], officeId: null, jobTitle: null, homeDisciplineId: null, partial: false, ...r,
})

/**
 * The planner's read: an empty grid on the 2026-10-12 week with the default thresholds, unless given.
 * @param {Partial<Schemas['GridResult']>} [r]
 * @returns {Schemas['GridResult']}
 */
const planningGrid = (r = {}) => ({
  weeks: [], currentWeek: '2026-10-12', evaluatedAt: '2026-10-18T00:00:00Z', partialView: false, draftAccess: false,
  settings: { horizonWeeks: 6, overPct: 110, underPct: 40, underWeeks: 1, staleDays: 14, maxHoursPerWeek: 40 }, people: [], ...r,
})

/**
 * What counted toward one person-week: a full week at 40 hours with nothing planned, unless given.
 * @param {Pick<Schemas['CellDetail'], 'personId' | 'personName' | 'week'> & Partial<Schemas['CellDetail']>} r
 * @returns {Schemas['CellDetail']}
 */
const cellDetail = (r) => ({
  partialView: false,
  capacity: { weeklyCapacity: 40, capacityOverride: false, workingDays: 5, capacity: 40, timeAway: 0, additionalAvailability: 0, days: [] },
  approvedAllocations: [], entries: [], bands: { confirmed: 0, expected: 0, possible: 0 }, remaining: 40, taskEstimates: { hours: 0, unestimatedTasks: 0 }, indicators: [], ...r,
})

/**
 * A page of entries, one page of everything unless given.
 * @param {Schemas['EntryRow'][]} items
 * @returns {Schemas['PageOfEntryRow']}
 */
const entryPage = (items) => ({ items, page: 1, pageSize: 50, totalCount: items.length })

/**
 * One edit of a plan entry, as the panel's history lists it.
 * @param {Pick<Schemas['ActivityEntry'], 'id' | 'actorId' | 'itemId'> & Partial<Schemas['ActivityEntry']>} r
 * @returns {Schemas['ActivityEntry']}
 */
const entryEdit = (r) => ({
  occurredAt: '2026-10-03T00:00:00Z', actorName: 'Sam Supervisor', actorType: 'User', projectId: null, itemType: 'PlanningEntry', itemKey: null, itemName: 'Planned work',
  action: 'Updated', categories: [], changes: [], summary: '', reason: null, source: 'Web', correlationId: null, snapshot: null, snapshotLabels: null, ...r,
})

module.exports = { planningEntry, planningCell, planningPerson, planningGrid, cellDetail, entryPage, entryEdit }
