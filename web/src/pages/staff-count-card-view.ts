/** Display-only intersections over the complete, already authorized Staff response. Never API filters. */
export const STAFF_CARD_INDICATORS = ['overdue', 'blocked', 'reviewsStalled'] as const
export type StaffCardIndicator = typeof STAFF_CARD_INDICATORS[number]
type Counts = { projects: number; overdue: number; blocked: number; reviewsStalled: number }

export function staffCardIndicators(raw: string | null) {
  const supplied = new Set((raw ?? '').split(',').map(value => value.trim()))
  return STAFF_CARD_INDICATORS.filter(value => supplied.has(value))
}

export function staffCardParams(sp: URLSearchParams, indicators: readonly StaffCardIndicator[]) {
  const next = new URLSearchParams(sp)
  const normalized = STAFF_CARD_INDICATORS.filter(value => indicators.includes(value))
  if (normalized.length) next.set('cardIndicators', normalized.join(',')); else next.delete('cardIndicators')
  return next
}

export function staffCardDestination(sp: URLSearchParams, indicator?: StaffCardIndicator) {
  const current = staffCardIndicators(sp.get('cardIndicators'))
  const next = staffCardParams(sp, indicator ? [...current, indicator] : current)
  return `/staff${next.size ? `?${next}` : ''}#staff-people`
}

const known = (value: unknown): value is number => typeof value === 'number' && Number.isFinite(value) && value >= 0

export function staffCardView<T extends Counts>(people: readonly T[], indicators: readonly StaffCardIndicator[]) {
  let unavailable = 0
  const rows = people.filter(person => {
    if (indicators.some(key => known(person[key]) && person[key] === 0)) return false
    if (indicators.some(key => !known(person[key]))) { unavailable++; return false }
    return true
  })
  const sum = (key: keyof Counts) => rows.every(row => known(row[key])) ? rows.reduce((n, row) => n + row[key], 0) : null
  const count = (key: StaffCardIndicator) => rows.every(row => known(row[key])) ? rows.filter(row => row[key] > 0).length : null
  return { rows, unavailable, tiles: { staff: rows.length, assignments: sum('projects'), withOverdue: count('overdue'), withBlocked: count('blocked'), reviewsStalled: sum('reviewsStalled') } }
}
