// @ts-check
/** Public synthetic wire data only. Provider capture/claim/transport/policy/privacy proof belongs to provider tests. */
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */
const { world: preferenceWorld } = require('./notice-preferences.fixture.cjs')
const { notificationRow, notificationPage, pulse } = require('./notifications.fixture.cjs')
const stamp = '2026-11-02T15:00:00Z'
const project = { id: '00000000-0000-0000-0000-000000099201', projectNumber: 'SYN-99201' }
const privateCanary = 'SYNTHETIC_PRIVATE_DELIVERY_METADATA_MUST_NOT_RENDER'
/** @param {Partial<Schemas['NotificationScreenDelivery']>} [overrides] @returns {Schemas['NotificationScreenDelivery']} */
const delivery = (overrides = {}) => ({ state: 'Queued', historyCoverage: 'Known', knownOccurrences: 1, totalOccurrences: 1, realEmailedOccurrences: 0, uncertainOccurrences: 0, ...overrides })
function world() {
  const preferences = preferenceWorld()
  /** These deliberately malformed public shapes are negative UI cases, not valid provider outcomes. */
  /** @type {{key:string; title:string; count:number; delivery:Schemas['NotificationScreenDelivery']}[]} */
  const definitions = [
    { key: 'real', title: 'Synthetic complete real receipt', count: 2, delivery: delivery({ state: 'Emailed', knownOccurrences: 2, totalOccurrences: 2, realEmailedOccurrences: 2, acceptedAt: stamp }) },
    { key: 'simulated', title: 'Synthetic simulation', count: 1, delivery: delivery({ state: 'Simulated' }) },
    { key: 'legacy', title: 'Synthetic legacy history unknown', count: 4, delivery: { state: 'Unknown', historyCoverage: 'Unavailable' } },
    { key: 'mixed', title: 'Synthetic mixed recorded and legacy occurrences', count: 3, delivery: delivery({ state: 'Mixed', historyCoverage: 'Partial', knownOccurrences: 2, totalOccurrences: 3 }) },
    { key: 'partialBulk', title: 'Synthetic partial bulk receipt', count: 1, delivery: delivery({ state: 'Partial' }) },
    { key: 'partialReal', title: 'Synthetic partial real and uncertain occurrences', count: 3, delivery: delivery({ state: 'Partial', knownOccurrences: 3, totalOccurrences: 3, realEmailedOccurrences: 1, uncertainOccurrences: 1 }) },
    { key: 'uncertain', title: 'Synthetic uncertain send outcome', count: 1, delivery: delivery({ state: 'Uncertain', uncertainOccurrences: 1 }) },
    { key: 'queued', title: 'Synthetic queued receipt awaiting pulse', count: 1, delivery: delivery() },
    { key: 'held', title: 'Synthetic held away', count: 1, delivery: delivery({ state: 'HeldAway' }) },
    { key: 'noAddress', title: 'Synthetic no email address', count: 1, delivery: delivery({ state: 'NoAddress' }) },
    { key: 'missingTime', title: 'Synthetic malformed emailed without time', count: 1, delivery: delivery({ state: 'Emailed', realEmailedOccurrences: 1 }) },
    { key: 'badTime', title: 'Synthetic malformed calendar timestamp', count: 1, delivery: delivery({ state: 'Emailed', realEmailedOccurrences: 1, acceptedAt: '2026-02-30T15:00:00Z' }) },
    { key: 'badCounts', title: 'Synthetic mismatched notification count', count: 1, delivery: delivery({ state: 'Emailed', knownOccurrences: 2, totalOccurrences: 2, realEmailedOccurrences: 2, acceptedAt: stamp }) },
    { key: 'badState', title: 'Synthetic unknown delivery state', count: 1, delivery: delivery({ state: privateCanary }) },
    { key: 'fractional', title: 'Synthetic fractional occurrence count', count: 1, delivery: delivery({ knownOccurrences: 0.5 }) },
    { key: 'conflicting', title: 'Synthetic contradictory emailed and uncertain', count: 1, delivery: delivery({ state: 'Emailed', realEmailedOccurrences: 0, uncertainOccurrences: 1, acceptedAt: stamp }) },
    { key: 'simulatedTime', title: 'Synthetic contradictory simulation with acceptance time', count: 1, delivery: delivery({ state: 'Simulated', acceptedAt: stamp }) },
  ]
  const ids = Object.fromEntries(definitions.map((entry, index) => [entry.key, '00000000-0000-0000-0000-' + String(99300 + index).padStart(12, '0')]))
  let version = 0
  const notices = () => definitions.map(entry => notificationRow({ id: ids[entry.key], title: entry.title, count: entry.count, projectId: project.id, projectNumber: project.projectNumber,
    itemType: 'Task', itemId: null, itemKey: null, linkPath: '/notifications?opened=' + ids[entry.key], createdAt: stamp, updatedAt: stamp, readAt: stamp }))
  return { ...preferences, ids, definitions, version: () => version, notices,
    pulse: () => pulse('synthetic-delivery:' + version),
    bumpPulse: () => { version++ },
    acceptQueued: () => { const entry = definitions.find(entry => entry.key === 'queued'); if (entry) entry.delivery = delivery({ state: 'Emailed', realEmailedOccurrences: 1, acceptedAt: stamp }); version++ },
    /** @param {URLSearchParams} query @returns {Schemas['NotificationScreenPage']} */
    screen: query => {
      const rows = notices().map((notification, index) => ({ notification, screen: { needsYourAction: null, contextState: 'Unknown',
        follow: { projectId: project.id, projectNumber: project.projectNumber, level: 'AllActivity', source: 'Manual' }, delivery: { ...definitions[index].delivery } } }))
      const selected = rows.filter(row => (query.get('unread') !== 'true' || !row.notification.readAt)
        && (!query.get('type') || query.get('type') === row.notification.eventType) && (!query.get('projectId') || query.get('projectId') === project.id) && query.get('needsAction') !== 'true')
      const page = Number(query.get('page') ?? 1), pageSize = Number(query.get('pageSize') ?? 50)
      return { items: selected.slice((page - 1) * pageSize, page * pageSize), page, pageSize, totalCount: selected.length, contextCoverage: { known: 0, unknown: rows.length, asOf: stamp } }
    },
    /** @param {URLSearchParams} query @returns {Schemas['PageOfNotificationRow']} */
    legacyNotices: query => { const rows = notices(), pageSize = Number(query.get('pageSize') ?? 50); return notificationPage(rows.slice(0, pageSize), { pageSize, totalCount: rows.length }) },
  }
}
module.exports = { world, stamp, project, privateCanary }
