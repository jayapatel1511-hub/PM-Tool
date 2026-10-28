// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */

/**
 * One calendar event typed against the contract, so a server rename or removal of a field fails `npm run typecheck:tests`.
 * Start and End are local to the organisation's zone; StartAt and EndAt are the same instants in UTC.
 * @param {string} id
 * @param {string} projectId
 * @param {Partial<Schemas['CalendarEventRow']>} [over]
 * @returns {Schemas['CalendarEventRow']}
 */
const calendarEvent = (id, projectId, over = {}) => ({
  kind: 'event', id, type: 'Meeting', title: 'Coordination meeting', start: '2026-10-14T09:00', end: '2026-10-14T10:00',
  startAt: '2026-10-14T12:00:00+00:00', endAt: '2026-10-14T13:00:00+00:00', timeZone: 'America/Halifax',
  projectId, projectNumber: 'P-100', projectName: 'Harbour survey', ownerId: '00000000-0000-0000-0000-000000000001', owner: 'Sam Supervisor',
  location: 'Boardroom', description: null, visibility: 'Project', cancelled: false, rowVersion: 3, canEdit: true, ...over,
})

/**
 * An all-day deadline projected from a task, deliverable or milestone date.
 * @param {string} id
 * @param {string} projectId
 * @returns {Schemas['CalendarEntryCalendarDeadline']}
 */
const calendarDeadline = (id, projectId) => ({
  kind: 'deadline', sourceType: 'Task', id, key: 'P-100-T0007', title: 'Survey review', date: '2026-10-13', projectId, projectNumber: 'P-100', projectName: 'Harbour survey', status: 'In Progress', overdue: false,
})

/**
 * A calendar range answer.
 * @param {string} from
 * @param {string} to
 * @param {Schemas['CalendarEntry'][]} entries
 * @returns {Schemas['CalendarRange']}
 */
const calendarRange = (from, to, entries) => ({ timeZone: 'America/Halifax', from, to, entries })

/**
 * The cancel answer: the event's id and its new row version.
 * @param {string} id
 * @param {number} rowVersion
 * @returns {Schemas['CalendarEventCancelled']}
 */
const calendarCancelled = (id, rowVersion) => ({ id, rowVersion })
module.exports = { calendarEvent, calendarDeadline, calendarRange, calendarCancelled }
