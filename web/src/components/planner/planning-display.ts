import { addDays, monthLabel, shortDate } from '@/lib/format'
import type { PlannerCell } from './labels'

export type PlannerUnit = 'hours' | 'percent'
export type PlannerZoom = 'weeks' | 'months'
export type PlannerColumn = { key: string; label: string; weeks: string[] }

/** Every ISO week belongs to the calendar month of its Thursday, including a November 30 Monday. */
export function planningColumns(weeks: string[], zoom: PlannerZoom): PlannerColumn[] {
  if (zoom === 'weeks') return weeks.map(week => ({ key: week, label: shortDate(week), weeks: [week] }))
  const months = new Map<string, string[]>()
  for (const week of weeks) {
    const key = addDays(week, 3).slice(0, 7)
    months.set(key, [...(months.get(key) ?? []), week])
  }
  return [...months].map(([key, list]) => ({ key, label: monthLabel(`${key}-01`), weeks: list }))
}

/** Aggregate the server's unrounded inputs, then format. Never average weekly percentages. */
export function planningTotals(cells: PlannerCell[]) {
  return cells.reduce((total, cell) => ({ capacity: total.capacity + cell.capacity, confirmed: total.confirmed + cell.confirmed,
    expected: total.expected + cell.expected, possible: total.possible + cell.possible, remaining: total.remaining + cell.remaining,
    timeAway: total.timeAway + cell.timeAway, provisional: total.provisional || cell.provisional }),
  { capacity: 0, confirmed: 0, expected: 0, possible: 0, remaining: 0, timeAway: 0, provisional: false })
}

/** PLN-09 planned hours exclude Possible. No percentage is meaningful with a zero/unknown denominator. */
export function plannedPercentage(planned: number, capacity: number) {
  return Number.isFinite(planned) && Number.isFinite(capacity) && capacity > 0 ? `${Math.round(planned / capacity * 1000) / 10} %` : null
}
