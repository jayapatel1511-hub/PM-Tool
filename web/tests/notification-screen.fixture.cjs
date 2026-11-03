// @ts-check
/** S45-A synthetic UI data, not an authorization or transport oracle. S47 is outside this fixture. */
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */
const { notificationRow, notificationPage } = require('./notifications.fixture.cjs')
/** @param {number} n */
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const project = { id: id(57001), projectNumber: 'SYN-57001' }, stamp = '2026-11-02T15:00:00Z'
const ids = { other: id(57101), first: id(57102), reviewer: id(57103), second: id(57104), unknown: id(57105) }
/** @param {Set<string>} read @returns {Schemas['NotificationRow'][]} */
const notices = read => [
  { id: ids.other, title: 'Synthetic other actor' }, { id: ids.first, title: 'Synthetic first action', count: 3 },
  { id: ids.reviewer, title: 'Synthetic reviewer action' }, { id: ids.second, title: 'Synthetic second action' },
  { id: ids.unknown, title: 'Synthetic context unavailable' },
].map((row, index) => notificationRow({ ...row, projectId: project.id, projectNumber: project.projectNumber, itemType: 'Task', itemId: id(57201 + index),
  linkPath: `/notifications?opened=${row.id}`, createdAt: stamp, updatedAt: stamp, readAt: read.has(row.id) ? stamp : null }))
/** @param {URLSearchParams} query @param {string} level @param {Set<string>} read @param {'all'|'unknown'|'knownOther'} [cohort] @returns {Schemas['NotificationScreenPage']} */
const screen = (query, level, read, cohort = 'all') => {
  const candidates = notices(read).filter(row => (cohort === 'all' || row.id === (cohort === 'unknown' ? ids.unknown : ids.other)) && (query.get('unread') !== 'true' || !row.readAt)
    && (!query.get('type') || row.eventType === query.get('type')) && (!query.get('projectId') || row.projectId === query.get('projectId')))
  /** @type {Schemas['NotificationScreenRow'][]} */
  const enhanced = candidates.map(notification => ({ notification, screen: {
    needsYourAction: notification.id === ids.unknown ? null : [ids.first, ids.second].includes(notification.id),
    contextState: notification.id === ids.unknown ? 'Unknown' : 'Known',
    follow: { projectId: project.id, projectNumber: project.projectNumber, level, source: 'Manual' }, delivery: { state: 'Unknown', historyCoverage: 'Unavailable' },
  } }))
  const selected = query.get('needsAction') === 'true' ? enhanced.filter(row => row.screen.needsYourAction === true) : enhanced
  const page = Number(query.get('page') ?? 1), pageSize = Number(query.get('pageSize') ?? 50), unknown = candidates.filter(row => row.id === ids.unknown).length
  return { items: selected.slice((page - 1) * pageSize, page * pageSize), page, pageSize, totalCount: selected.length,
    contextCoverage: { known: candidates.length - unknown, unknown, asOf: stamp } }
}
/** @param {Set<string>} read */
const legacy = read => notificationPage(notices(read))
module.exports = { id, ids, project, stamp, notices, screen, legacy }
