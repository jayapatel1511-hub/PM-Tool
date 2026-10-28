// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */

/**
 * What the signed-in person may do. Everything is off unless given, as the server sends it for a person with no roles.
 * @param {Partial<Schemas['MeCapabilities']>} [c]
 * @returns {Schemas['MeCapabilities']}
 */
const meCapabilities = (c = {}) => ({
  createProject: false, createTask: false, portfolio: false, workload: false, staff: false, allStaff: false, admin: false, templates: false, readOnly: false, directReports: 0, ...c,
})

/**
 * The organisation settings the SPA reads from GET /me. The four settings behind a flag are left out unless a suite gives them,
 * as the server leaves them out while the flag is off.
 * @param {Partial<Schemas['CurrentSettings']>} [s]
 * @returns {Schemas['CurrentSettings']}
 */
const meSettings = (s = {}) => ({
  dateFormat: 'yyyy-MM-dd', orgTimeZone: 'America/Halifax', today: '2026-10-18', idleTimeoutHours: 8, restrictedProjectsEnabled: false, allowSelfReview: false, taskDueSoonDays: 5,
  milestoneApproachingDays: 14, chainDepthLimit: 10, defaultWeeklyCapacityHours: 40, coordinationLookaheadWeeks: 3, workingDaysEnabled: false, ...s,
})

/**
 * The person's own preferences.
 * @param {Partial<Schemas['MePreferences']>} [p]
 * @returns {Schemas['MePreferences']}
 */
const mePreferences = (p = {}) => ({ digestEnabled: true, digestTimeLocal: null, denseRows: true, weeklySummaryEnabled: true, ...p })

/**
 * GET /me, typed against the contract so a server rename of a field fails `npm run typecheck:tests`. A person with no roles has no
 * capabilities, no job title, office or supervisor (sent as null) and no flags on; a suite gives only what its checks read.
 * @param {Pick<Schemas['MeView'], 'id' | 'displayName' | 'email'> & Partial<Omit<Schemas['MeView'], 'capabilities' | 'settings' | 'preferences'>> & {
 *   capabilities?: Partial<Schemas['MeCapabilities']>, settings?: Partial<Schemas['CurrentSettings']>, preferences?: Partial<Schemas['MePreferences']> }} r
 * @returns {Schemas['MeView']}
 */
const meView = ({ capabilities, settings, preferences, ...r }) => ({
  jobTitle: null, officeId: null, supervisorId: null, isTemplateEditor: false, roles: [], systemRoles: [], flags: {}, ...r,
  capabilities: meCapabilities(capabilities), settings: meSettings(settings), preferences: mePreferences(preferences),
})

/**
 * One discipline of the reference data, active unless a suite says otherwise.
 * @param {string} id
 * @param {string} name
 * @param {Partial<Schemas['ReferenceDiscipline']>} [r]
 * @returns {Schemas['ReferenceDiscipline']}
 */
const referenceDiscipline = (id, name, r = {}) => ({ id, name, code: name.slice(0, 3).toUpperCase(), colour: '#64748b', isActive: true, sortOrder: 0, ...r })

/**
 * @param {string} id
 * @param {string} name
 * @param {Partial<Schemas['ReferenceClient']>} [r]
 * @returns {Schemas['ReferenceClient']}
 */
const referenceClient = (id, name, r = {}) => ({ id, name, shortName: null, isActive: true, ...r })

/**
 * @param {string} id
 * @param {string} name
 * @param {Partial<Schemas['ReferenceOffice']>} [r]
 * @returns {Schemas['ReferenceOffice']}
 */
const referenceOffice = (id, name, r = {}) => ({ id, name, code: name.slice(0, 2).toUpperCase(), timeZone: 'America/Halifax', isActive: true, ...r })

/**
 * @param {string} id
 * @param {string} name
 * @param {Partial<Schemas['ReferenceDeliverableType']>} [r]
 * @returns {Schemas['ReferenceDeliverableType']}
 */
const referenceDeliverableType = (id, name, r = {}) => ({ id, name, defaultDisciplineId: null, isActive: true, ...r })

/**
 * GET /reference. Every list is present and empty unless given, and the role label limit is the one the database enforces.
 * @param {Partial<Schemas['ReferenceView']>} [r]
 * @returns {Schemas['ReferenceView']}
 */
const referenceData = (r = {}) => ({ disciplines: [], clients: [], offices: [], deliverableTypes: [], phases: [], projectTypes: [], limits: { roleLabel: 80 }, ...r })

module.exports = { meCapabilities, meSettings, mePreferences, meView, referenceDiscipline, referenceClient, referenceOffice, referenceDeliverableType, referenceData }
