// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */
const { meView } = require('./me.fixture.cjs')
const { homeRead } = require('./workspace.fixture.cjs')
const viewerId = '00000000-0000-0000-0000-000000099101'
/** @param {Partial<Schemas['NoticePreferencesRead']>} [overrides] @returns {Schemas['NoticePreferencesRead']} */
const noticePreferences = (overrides = {}) => ({ revision: 0, noticeDigestFrequency: 'Daily', holdImmediateEmailWhenAway: false,
  timeReminderEnabled: false, timeReminderWeekday: null, timeReminderTimeLocal: null, ...overrides })
/** Schema-valid negative reply: it contradicts omitted fields while retaining the requested changed field/revision. */
/** @param {Schemas['NoticePreferencesRead']} value @returns {Schemas['NoticePreferencesRead']} */
const contradictoryReply = value => ({ ...value, noticeDigestFrequency: value.noticeDigestFrequency === 'Daily' ? 'Weekly' : 'Daily',
  timeReminderWeekday: value.timeReminderWeekday === 1 ? 2 : 1, timeReminderTimeLocal: value.timeReminderTimeLocal === '09:00' ? '10:00' : '09:00' })
/** Existing digest settings remain independently served and must never be written by the new form. */
/** @returns {Schemas['PreferencesRead']} */
const preferences = () => ({ digest: { digestEnabled: true, holdDigestWhenAway: true, managesProjects: false, orgTime: '08:00', sections: [], sectionsOff: [], time: '08:00', weekendDigests: false, weeklySummaryEnabled: false }, events: [], follows: [] })
/** Empty permitted Home scope, only for a real navigation away from and back to Preferences. */
/** @returns {Schemas['HomeRead']} */
const home = () => homeRead({ projectId: viewerId, today: '2026-11-02', from: '2026-11-02', to: '2026-11-15', asOf: '2026-11-02T15:00:00Z',
  metrics: { activeProjects: 0, openTasks: 0, overdueTasks: 0, onTrack: { green: 0, evaluated: 0, pct: null }, teamMembers: 0 },
  tasksByStatus: [], onHold: { status: 'On Hold', count: 0 }, tasksByProject: [] })
/** @param {{people?: boolean, work?: boolean, capacity?: boolean, readOnly?: boolean}} [options] */
function world(options = {}) {
  const flags = { people_screens: options.people !== false, work_screens: options.work ?? false, capacity_screens: options.capacity ?? false,
    data_entry_safety: true, screen_patterns: false, visual_system: false, small_ux: false, live_events: false, onboarding: false, onboarding_card: false, org_structure: false }
  const actor = meView({ id: viewerId, displayName: 'Synthetic Riley Preferences', email: 'synthetic.riley@example.test', systemRoles: options.readOnly ? ['ReadOnly'] : ['StandardUser'],
    capabilities: { readOnly: options.readOnly ?? false }, flags, settings: { today: '2026-11-02', orgTimeZone: 'America/Halifax' } })
  let current = noticePreferences()
  return { actor, flags, legacy: preferences(), home: home(),
    current: () => ({ ...current }),
    /** @param {Partial<Schemas['NoticePreferencesRead']>} patch */
    change: patch => { current = { ...current, ...patch } },
    /** @param {Schemas['NoticePreferencesSetBody']} body @returns {Schemas['NoticePreferencesRead']} */
    apply: body => {
      const next = { ...current, noticeDigestFrequency: body.noticeDigestFrequency ?? current.noticeDigestFrequency,
        holdImmediateEmailWhenAway: body.holdImmediateEmailWhenAway ?? current.holdImmediateEmailWhenAway, timeReminderEnabled: body.timeReminderEnabled ?? current.timeReminderEnabled,
        timeReminderWeekday: body.timeReminderWeekday ?? current.timeReminderWeekday, timeReminderTimeLocal: body.timeReminderTimeLocal ?? current.timeReminderTimeLocal }
      const changed = next.noticeDigestFrequency !== current.noticeDigestFrequency || next.holdImmediateEmailWhenAway !== current.holdImmediateEmailWhenAway
        || next.timeReminderEnabled !== current.timeReminderEnabled || next.timeReminderWeekday !== current.timeReminderWeekday || next.timeReminderTimeLocal !== current.timeReminderTimeLocal
      current = { ...next, revision: current.revision + (changed ? 1 : 0) }
      return { ...current }
    },
  }
}
module.exports = { viewerId, noticePreferences, contradictoryReply, preferences, world }
