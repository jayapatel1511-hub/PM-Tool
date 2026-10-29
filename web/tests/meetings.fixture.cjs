// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */

/**
 * One meeting in the register: a coordination meeting on 1 October with no notes, no calendar event and no actions unless given.
 * @param {Pick<Schemas['MeetingListRow'], 'id' | 'projectId' | 'title'> & Partial<Schemas['MeetingListRow']>} r
 * @returns {Schemas['MeetingListRow']}
 */
const meetingRow = (r) => ({
  meetingDate: '2026-10-01', meetingType: 'Coordination', notesLink: null, calendarEventId: null, calendarEventTitle: null, open: 0, total: 0, rowVersion: 1, ...r,
})

/**
 * One action as the register, My Work and the coordination view read it: owned by a person (Alex Chen) with no due date, open and not converted unless given.
 * @param {Pick<Schemas['MeetingActionRow'], 'id' | 'projectId' | 'key' | 'meetingId' | 'text'> & Partial<Schemas['MeetingActionRow']>} r
 * @returns {Schemas['MeetingActionRow']}
 */
const actionRow = (r) => ({
  projectNumber: 'P-MOCK', meetingTitle: 'Synthetic meeting', meetingDate: '2026-10-01', ownerType: 'User', ownerTypeLabel: 'Person', ownerUserId: null, ownerDisciplineId: null,
  ownerExternalPartyId: null, ownerName: 'Alex Chen', leadId: null, leadName: null, noLead: false, partyIsClient: false, dueDate: null, isOverdue: false, daysOverdue: 0,
  status: 'Open', relatedTaskId: null, taskKey: null, relatedDecisionId: null, decisionKey: null, converted: false, rowVersion: 1, ...r,
})

/**
 * The action panel's read: the caller may edit and comment, there is nothing to move it to and nothing related unless given.
 * @param {Pick<Schemas['ActionDetail'], 'action'> & Partial<Schemas['ActionDetail']>} r
 * @returns {Schemas['ActionDetail']}
 */
const actionDetail = (r) => ({
  meeting: { id: r.action.meetingId, title: r.action.meetingTitle ?? 'Synthetic meeting', meetingDate: r.action.meetingDate, meetingType: 'Coordination', notesLink: null },
  project: { id: r.action.projectId, projectNumber: r.action.projectNumber ?? 'P-MOCK', name: 'Synthetic live project', status: 'Active' },
  task: null, decision: null, links: [], taskDisciplineId: null,
  permissions: { edit: { ok: true, reason: null }, transitions: [], convert: false, comment: true }, ...r,
})

/**
 * The stored meeting, as the create and edit routes answer it: the register's row without the event title and the counts.
 * @param {Schemas['MeetingListRow']} r
 * @returns {Schemas['MeetingRead']}
 */
const meetingRead = (r) => ({ id: r.id, projectId: r.projectId, title: r.title, meetingDate: r.meetingDate, meetingType: r.meetingType, notesLink: r.notesLink, calendarEventId: r.calendarEventId, rowVersion: r.rowVersion })

/**
 * The answers of the action commands: the new action, an edit, a move and a conversion, each with the row version the server wrote.
 * @param {string} id
 * @param {string} key
 * @returns {Schemas['ActionCreated']}
 */
const actionCreated = (id, key) => ({ id, key, rowVersion: 0, warnings: [] })
/** @param {string} id @param {number} rowVersion @returns {Schemas['ActionMutation']} */
const actionMutation = (id, rowVersion) => ({ id, rowVersion })
/** @param {string} id @param {string} status @param {number} rowVersion @returns {Schemas['ActionMoved']} */
const actionMoved = (id, status, rowVersion) => ({ id, status, rowVersion })
/** @param {string} id @param {string} status @param {number} rowVersion @param {string} taskId @param {string} taskKey @returns {Schemas['ActionConverted']} */
const actionConverted = (id, status, rowVersion, taskId, taskKey) => ({ id, status, rowVersion, taskId, taskKey })

module.exports = { meetingRow, meetingRead, actionRow, actionDetail, actionCreated, actionMutation, actionMoved, actionConverted }
