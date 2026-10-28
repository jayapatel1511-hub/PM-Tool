// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */

/**
 * One ranked attention row typed against the contract, so a server rename of a field fails `npm run typecheck:tests`.
 * Every nullable field is spelled out: an item that is not snoozed carries snoozedUntil and snoozeNote as null, as the page receives them.
 * @param {{ id: string, projectId: string, itemId: string } & Partial<Schemas['AttentionRow']>} row
 * @returns {Schemas['AttentionRow']}
 */
const attentionRow = (row) => ({
  projectNumber: 'P-DEMO', ruleId: 'A-03', ruleName: 'Overdue predecessor blocking others', itemType: 'Task', itemKey: 'P-DEMO-T0001', itemName: 'Synthetic overdue task',
  severity: 'Critical', message: 'Overdue and blocking 1 task', why: { text: 'A-03 Overdue predecessor blocking others: due 2026-10-10, blocking 1 task', rule: 'A-03', colour: 'Critical', threshold: 1, values: { blocking: 1 } },
  routeToUserIds: [], ownerUserId: null, ownerName: 'Synthetic owner', projectDisciplineId: null, daysOverdueOrBlocked: 8, priority: 'Medium', dueDate: '2026-10-10',
  firstDetectedAt: '2026-10-11T12:00:00Z', ageDays: 7, snoozed: false, snoozedUntil: null, snoozeNote: null,
  ...row,
})

/**
 * A project's attention answer. `total` counts the rows that are not snoozed and `snoozed` the hidden ones, as the server does.
 * @param {Schemas['AttentionRow'][]} items
 * @param {number} [snoozed]
 * @returns {Schemas['AttentionList']}
 */
const attentionList = (items, snoozed = 0) => ({ items, snoozed, total: items.filter((x) => !x.snoozed).length, truncated: false })

/**
 * The snooze answer, and the health override's answers: the stored expiry on a save, the new row version on a clear.
 * @param {string} id
 * @param {string} until
 * @returns {Schemas['AttentionSnoozed']}
 */
const attentionSnoozed = (id, until) => ({ id, snoozedUntil: until })
/**
 * @param {string} id
 * @param {number} rowVersion
 * @param {string} expires
 * @returns {Schemas['HealthOverrideApplied']}
 */
const healthOverrideApplied = (id, rowVersion, expires) => ({ id, rowVersion, healthOverrideExpiresAt: expires })
/**
 * @param {string} id
 * @param {number} rowVersion
 * @returns {Schemas['HealthOverrideRemoved']}
 */
const healthOverrideRemoved = (id, rowVersion) => ({ id, rowVersion })

module.exports = { attentionRow, attentionList, attentionSnoozed, healthOverrideApplied, healthOverrideRemoved }
