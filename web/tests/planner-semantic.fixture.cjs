// Synthetic inputs for the API-free planner presentation harness; no operational records.
const { planningEntry, planningCell, planningPerson, planningGrid, cellDetail } = require('./planning.fixture.cjs')
const { availabilityRead, availabilityPerson } = require('./availability.fixture.cjs')

/** @param {number} n */
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const weeks = ['2026-10-05', '2026-10-12']
const sources = ['MajorProject', 'OtherProject', 'Proposal', 'BusinessDevelopment', 'Admin', 'Supervision', 'Training', 'InternalInitiative', 'FieldWork', 'Other']
const entries = sources.map((sourceCategory, i) => planningEntry({
  id: id(200 + i), personId: id(101), personName: 'Reported away', ownerId: i === 0 ? id(101) : id(1), ownerName: i === 0 ? 'Reported away' : 'Synthetic owner',
  label: `Recorded source ${i + 1}`, sourceCategory, startWeek: weeks[0], endWeek: weeks[0],
  confidence: i < 3 ? 'Confirmed' : i < 6 ? 'Expected' : 'Possible', hoursPerWeek: i < 6 ? 2 : 1,
  ownerKind: i === 0 ? 'Self' : 'Manager', visibility: i === 0 ? 'Confirmed' : i === 1 ? 'Published' : 'Draft',
  projectId: i === 0 ? id(501) : null, projectNumber: i === 0 ? 'SYN-501' : null,
  projectName: i === 0 ? 'Synthetic linked project' : null, matchesFilter: i !== 6, canEdit: i !== 0, canChangeVisibility: i !== 0,
}))
const away = planningPerson({ id: id(101), displayName: 'Reported away', supervisorId: id(1), supervisorName: 'Synthetic owner', canCreateManagerEntry: true, entries,
  cells: [planningCell({ week: weeks[0], capacity: 36, timeAway: 4, confirmed: 6, expected: 6, possible: 4, remaining: 24, ownDraftHours: 12 }), planningCell({ week: weeks[1] })] })
const zero = planningPerson({ id: id(102), displayName: 'Zero without away',
  cells: [planningCell({ week: weeks[0], capacity: 0, holidayHours: 40, remaining: 0 }), planningCell({ week: weeks[1], capacity: 48, additionalAvailability: 8, remaining: 48 })] })
const provisional = planningPerson({ id: id(103), displayName: 'Provisional away',
  cells: [planningCell({ week: weeks[0], capacity: 32, timeAway: 8, provisional: true, remaining: 32 }), planningCell({ week: weeks[1], provisional: true })] })
const partial = planningPerson({ id: id(104), displayName: 'Partial planning', partial: true,
  cells: [planningCell({ week: weeks[0] }), planningCell({ week: weeks[1] })] })
const approved = planningPerson({ id: id(105), displayName: 'Approved allocation',
  cells: [planningCell({ week: weeks[0], approved: 8, confirmed: 8, remaining: 32 }), planningCell({ week: weeks[1] })],
  approvedAllocations: [{ allocationId: id(601), projectId: id(501), projectNumber: 'SYN-501', projectName: 'Synthetic linked project', approvalStatus: 'Confirmed', canOpen: false, weeks: [{ week: weeks[0], hours: 8 }] }] })
const data = planningGrid({ weeks, currentWeek: weeks[0], evaluatedAt: '2026-10-06T12:00:00Z', people: [away, zero, provisional, partial, approved] })

// Explicit server-like facts: a working Saturday, known zero working date, unknown calendar,
// holiday, partial absence and provisional absence. Tests must not infer from their numbers/dates.
const days = [
  { date: '2026-10-05', normal: 8, available: 4, timeAway: 4, holiday: 0, additional: 0, category: 'Unavailable', provisional: false, nonWorking: false },
  { date: '2026-10-06', normal: 8, available: 0, timeAway: 0, holiday: 8, additional: 0, category: null, provisional: false, nonWorking: true },
  { date: '2026-10-07', normal: 0, available: 0, timeAway: 0, holiday: 0, additional: 0, category: null, provisional: false, nonWorking: false },
  { date: '2026-10-08', normal: 0, available: 0, timeAway: 0, holiday: 0, additional: 0, category: null, provisional: false },
  { date: '2026-10-09', normal: 8, available: 0, timeAway: 8, holiday: 0, additional: 0, category: 'Unavailable', provisional: true, nonWorking: null },
  { date: '2026-10-10', normal: 8, available: 8, timeAway: 0, holiday: 0, additional: 0, category: null, provisional: false, nonWorking: false },
  { date: '2026-10-11', normal: 0, available: 0, timeAway: 0, holiday: 0, additional: 0, category: null, provisional: false, nonWorking: true },
]
const detail = cellDetail({ personId: id(701), personName: 'Daily calendar cases', week: weeks[0],
  capacity: { weeklyCapacity: 40, capacityOverride: true, workingDays: 4, capacity: 12, timeAway: 12, additionalAvailability: 0, days },
  remaining: 12,
})
const yearDays = []
for (let date = new Date('2026-01-01T00:00:00Z'); date.getUTCFullYear() === 2026; date.setUTCDate(date.getUTCDate() + 1)) {
  const day = date.toISOString().slice(0, 10)
  const exact = days.find(item => item.date === day)
  // These are labelled synthetic server inputs, not a production client calendar rule.
  const working = date.getUTCDay() >= 1 && date.getUTCDay() <= 5
  const value = exact ?? { date: day, normal: working ? 8 : 0, available: working ? 8 : 0, timeAway: 0, holiday: 0, provisional: false, nonWorking: !working }
  yearDays.push({ date: value.date, normal: value.normal, available: value.available, leave: value.timeAway, holiday: value.holiday, provisional: value.provisional,
    ...('nonWorking' in value ? { nonWorking: value.nonWorking } : {}) })
}
/** @param {number} n @param {string} date @param {number | null} hoursPerDay */
const leaveRecord = (n, date, hoursPerDay) => ({ id: id(n), fromDate: date, throughDate: date, hoursPerDay, status: 'Approved', sourceKind: 'HRImport', sourceSystem: null, externalId: null, sourceUpdatedAt: '2026-10-01T12:00:00Z', importedAt: '2026-10-01T13:00:00Z', rowVersion: 1, canEdit: false })
const person = availabilityPerson({ id: detail.personId, displayName: detail.personName, canManage: false, weeklyHours: null, schedules: null,
  days: yearDays, leave: [leaveRecord(801, '2026-10-05', 4), leaveRecord(802, '2026-10-09', null)],
  unverified: [{ workDate: '2026-10-09', availableHours: 0, rowVersion: 1 }],
})
const availability = availabilityRead({ canImport: false, people: [person], holidays: [{ date: '2026-10-06', name: 'Synthetic office holiday', officeId: null }] })
module.exports = { id, weeks, sources, entries, data, days, detail, availability, kept: [entries[6].id] }
