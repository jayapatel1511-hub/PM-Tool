// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */

/**
 * The pulse the bell polls: a stamp that changes when something arrives, and a project stamp only when one was asked for.
 * @param {string} stamp
 * @param {string} [projectStamp]
 * @returns {Schemas['NotificationPulse']}
 */
const pulse = (stamp, projectStamp) => (projectStamp === undefined ? { stamp } : { stamp, projectStamp })

/** @param {number} notifications @param {number} following @returns {Schemas['UnreadCount']} */
const unreadCount = (notifications, following) => ({ notifications, following })

/** @param {number} marked @returns {Schemas['MarkedRead']} */
const markedRead = (marked) => ({ marked })

/**
 * One personal notification: a task assigned to the person, unread, with no project, item, link or actor unless given. Nulls are sent as the server sends them.
 * @param {Pick<Schemas['NotificationRow'], 'id' | 'title'> & Partial<Schemas['NotificationRow']>} r
 * @returns {Schemas['NotificationRow']}
 */
const notificationRow = (r) => ({
  eventType: 'TaskAssigned', projectId: null, projectNumber: null, itemType: null, itemId: null, itemKey: null, body: null, linkPath: null, actorUserId: null, actorName: null,
  count: 1, createdAt: '2026-10-03T09:00:00Z', updatedAt: '2026-10-03T09:00:00Z', readAt: null, ...r,
})

/**
 * A page of notifications; the total is the number of rows unless given.
 * @param {Schemas['NotificationRow'][]} items
 * @param {{ page?: number, pageSize?: number, totalCount?: number }} [o]
 * @returns {Schemas['PageOfNotificationRow']}
 */
const notificationPage = (items, { page = 1, pageSize = 50, totalCount = items.length } = {}) => ({ items, page, pageSize, totalCount })

/**
 * One event's channels: both on, no personal choice and not a direct assignment unless given.
 * @param {Pick<Schemas['PreferenceEvent'], 'code'> & Partial<Schemas['PreferenceEvent']>} r
 * @returns {Schemas['PreferenceEvent']}
 */
const preferenceEvent = (r) => ({ app: true, email: true, defaultApp: true, defaultEmail: true, direct: false, custom: false, ...r })

/**
 * The Preferences read: a daily digest at 08:00 with every section on and the weekly summary off, for someone who manages no project, unless given.
 * @param {{ events?: Schemas['PreferenceEvent'][], digest?: Partial<Schemas['PreferenceDigest']>, follows?: Schemas['PreferenceFollow'][] }} [r]
 * @returns {Schemas['PreferencesRead']}
 */
const preferences = ({ events = [], digest = {}, follows = [] } = {}) => ({
  events, follows,
  digest: { digestEnabled: true, holdDigestWhenAway: false, time: '08:00', orgTime: '08:00', weekendDigests: false, sections: [], sectionsOff: [], weeklySummaryEnabled: false, managesProjects: false, ...digest },
})

/**
 * A followed project in the Following list: Active, never read and with nothing unread unless given.
 * @param {Pick<Schemas['FollowingProject'], 'projectId' | 'projectNumber' | 'name'> & Partial<Schemas['FollowingProject']>} r
 * @returns {Schemas['FollowingProject']}
 */
const followingProject = (r) => ({ status: 'Active', lastSeenAt: null, unread: 0, ...r })

/**
 * One feed entry: a change by someone else to a task, one change, not notified and unread unless given.
 * @param {{ entry: Schemas['ActivityEntry'] } & Partial<Schemas['FollowingItem']>} r
 * @returns {Schemas['FollowingItem']}
 */
const followingItem = (r) => ({ count: 1, projectNumber: 'P-MOCK', projectName: 'Synthetic live project', notified: false, unread: true, ...r })

/**
 * The Following feed: the followed projects and one page of entries, the total being the number of entries unless given.
 * @param {Schemas['FollowingProject'][]} projects
 * @param {Schemas['FollowingItem'][]} items
 * @param {{ page?: number, pageSize?: number, totalCount?: number }} [o]
 * @returns {Schemas['FollowingFeed']}
 */
const followingFeed = (projects, items, { page = 1, pageSize = 200, totalCount = items.length } = {}) => ({ projects, items, page, pageSize, totalCount })

/** @param {string} level @param {string} [source] @returns {Schemas['FollowRead']} */
const followRead = (level, source = 'Manual') => ({ level, source })

module.exports = { pulse, unreadCount, markedRead, notificationRow, notificationPage, preferenceEvent, preferences, followingProject, followingItem, followingFeed, followRead }
