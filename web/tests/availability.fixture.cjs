// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */

/**
 * One day of a person's capacity: a full contracted day, nothing taken, unless given.
 * @param {Pick<Schemas['AvailabilityDay'], 'date'> & Partial<Schemas['AvailabilityDay']>} r
 * @returns {Schemas['AvailabilityDay']}
 */
const availabilityDay = (r) => ({ normal: 8, available: 8, leave: 0, holiday: 0, provisional: false, ...r })

/**
 * A person the caller manages, on 40 contracted hours with no schedule rows, leave or unverified days unless given.
 * @param {Pick<Schemas['AvailabilityPerson'], 'id' | 'displayName'> & Partial<Schemas['AvailabilityPerson']>} r
 * @returns {Schemas['AvailabilityPerson']}
 */
const availabilityPerson = (r) => ({ canManage: true, weeklyHours: 40, days: [], schedules: [], leave: [], unverified: [], ...r })

/**
 * Planned work that falls on a day the person is away: a task they assign, due on it, unavailable unless given.
 * @param {Pick<Schemas['AvailabilityConcern'], 'personId' | 'id' | 'projectId'> & Partial<Schemas['AvailabilityConcern']>} r
 * @returns {Schemas['AvailabilityConcern']}
 */
const availabilityConcern = (r) => ({ key: 'T-001', name: 'Synthetic coverage task', type: 'Task', dueDate: '2026-10-12', role: 'Assignee', concern: 'Unavailable', ...r })

/**
 * The Vacation page's read: October 2026, an administrator who can import, with nobody listed and no concerns unless given.
 * @param {Partial<Schemas['AvailabilityRead']>} [r]
 * @returns {Schemas['AvailabilityRead']}
 */
const availabilityRead = (r = {}) => ({ from: '2026-10-01', through: '2026-10-31', canImport: true, people: [], holidays: [], coverage: [], imports: [], ...r })

/**
 * A person with an HR source and key, as the import dialog's mapping list shows them.
 * @param {Pick<Schemas['HrIdentity'], 'id' | 'displayName'> & Partial<Schemas['HrIdentity']>} r
 * @returns {Schemas['HrIdentity']}
 */
const hrIdentity = (r) => ({ hrSourceSystem: 'Synthetic HR', hrPersonKey: 'employee-1', rowVersion: 1, ...r })

/**
 * A reviewed full-day leave row for a person, not yet imported.
 * @param {Pick<Schemas['AvailabilityImportRow'], 'externalId' | 'personId' | 'fromDate' | 'throughDate'> & Partial<Schemas['AvailabilityImportRow']>} r
 * @returns {Schemas['AvailabilityImportRow']}
 */
const importRow = (r) => ({
  sourceSystem: 'Synthetic HR', personKey: 'employee-1', personVersion: 1, hoursPerDay: null, status: 'Approved', sourceUpdatedAt: '2026-10-01T00:00:00Z', rowVersion: 0, ...r,
})

/**
 * A clean import preview with nothing created, changed or cancelled unless given.
 * @param {Pick<Schemas['LeaveImportPreviewRead'], 'previewId' | 'rows'> & Partial<Schemas['LeaveImportPreviewRead']>} r
 * @returns {Schemas['LeaveImportPreviewRead']}
 */
const leaveImportPreview = (r) => ({ errors: [], canApply: true, creates: 0, updates: 0, cancellations: 0, unchanged: 0, manualMatches: 0, affectedWeeks: [], ...r })

/**
 * The answer to applying a reviewed import.
 * @param {Partial<Schemas['LeaveImportApplied']>} [r]
 * @returns {Schemas['LeaveImportApplied']}
 */
const leaveImportApplied = (r = {}) => ({ applied: 1, unchanged: 0, importedAt: '2026-10-03T12:00:00Z', ...r })

/**
 * The answer to saving time away or a working schedule: the record's id at its first version.
 * @param {string} id
 * @returns {Schemas['LeaveSaved'] & Schemas['ScheduleSaved']}
 */
const recordSaved = (id) => ({ id, rowVersion: 1 })

module.exports = { availabilityDay, availabilityPerson, availabilityConcern, availabilityRead, hrIdentity, importRow, leaveImportPreview, leaveImportApplied, recordSaved }
