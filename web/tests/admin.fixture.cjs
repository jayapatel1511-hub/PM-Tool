// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */

/**
 * The shared columns of any reference entry: active, in sort position 0, audited by the system, written at one fixed time with
 * a null project and creator as the server sends them.
 * @param {string} kindName the entity's type name, which is also its audit key
 * @param {Pick<Schemas['ReferenceRow'], 'id' | 'name'> & Partial<Schemas['ReferenceRow']>} r
 * @returns {Schemas['ReferenceRow']}
 */
const referenceRow = (kindName, r) => ({
  isActive: true, sortOrder: 0, auditType: 'ReferenceData', auditProjectId: null, auditKey: kindName, auditName: r.name,
  createdAt: '2026-10-01T09:00:00Z', createdBy: null, updatedAt: '2026-10-01T09:00:00Z', updatedBy: null, rowVersion: 1, ...r,
})

/**
 * An office: its code and time zone first, as the server writes them.
 * @param {Pick<Schemas['OfficeRow'], 'id' | 'name'> & Partial<Schemas['OfficeRow']>} r
 * @returns {Schemas['OfficeRow']}
 */
const officeRow = (r) => ({ code: r.name.slice(0, 3).toUpperCase(), timeZone: 'America/Halifax', ...referenceRow('Office', r), ...r })

/**
 * A discipline with its code and colour.
 * @param {Pick<Schemas['DisciplineRow'], 'id' | 'name'> & Partial<Schemas['DisciplineRow']>} r
 * @returns {Schemas['DisciplineRow']}
 */
const disciplineRow = (r) => ({ code: r.name.slice(0, 3).toUpperCase(), colour: '#2563eb', ...referenceRow('Discipline', r), ...r })

/**
 * A client; its short name is sent as null when it has none.
 * @param {Pick<Schemas['ClientRow'], 'id' | 'name'> & Partial<Schemas['ClientRow']>} r
 * @returns {Schemas['ClientRow']}
 */
const clientRow = (r) => ({ shortName: null, ...referenceRow('Client', r), ...r })

/**
 * A holiday: the organisation's own (no office) unless an office is given.
 * @param {Pick<Schemas['HolidayRow'], 'id' | 'date' | 'name'> & Partial<Schemas['HolidayRow']>} r
 * @returns {Schemas['HolidayRow']}
 */
const holidayRow = (r) => ({ officeId: null, office: null, ...r })

/**
 * A setting row; a flag row also names its packet.
 * @param {Pick<Schemas['SettingRow'], 'key' | 'kind' | 'group' | 'default'> & Partial<Schemas['SettingRow']>} r
 * @returns {Schemas['SettingRow']}
 */
const settingRow = (r) => ({ value: r.default, ...r })

/**
 * A person as the Admin Users list shows them: active, with no job title, office, supervisor, home discipline or capacity,
 * never signed in and with no roles unless given.
 * @param {Pick<Schemas['AdminUserRow'], 'id' | 'displayName'> & Partial<Schemas['AdminUserRow']>} r
 * @returns {Schemas['AdminUserRow']}
 */
const adminUser = (r) => ({
  email: `${r.displayName.toLowerCase().replace(/\W+/g, '.')}@example.test`, jobTitle: null, officeId: null, supervisorId: null, homeDisciplineId: null,
  isActive: true, weeklyCapacityHours: null, isTemplateEditor: false, lastSignInAt: null, rowVersion: 1, roles: [], ...r,
})

/**
 * What Operations reads: nothing pending, no problems, no jobs and no dead letters unless given.
 * @param {Partial<Schemas['OperationsRead']>} [r]
 * @returns {Schemas['OperationsRead']}
 */
const operationsRead = (r = {}) => ({ asOf: '2026-10-18T22:00:00Z', problems: [], jobs: [], pendingChanges: 0, deadLetters: [], deadLetterCount: 0, ...r })

/**
 * One dead-lettered change; no project, no last error.
 * @param {Pick<Schemas['OpsDeadLetter'], 'id' | 'eventType'> & Partial<Schemas['OpsDeadLetter']>} r
 * @returns {Schemas['OpsDeadLetter']}
 */
const opsDeadLetter = (r) => ({ projectNumber: null, attempts: 10, lastError: null, createdAt: '2026-10-18T21:00:00Z', deadLetteredAt: '2026-10-18T21:30:00Z', ...r })

module.exports = { referenceRow, officeRow, disciplineRow, clientRow, holidayRow, settingRow, adminUser, operationsRead, opsDeadLetter }
