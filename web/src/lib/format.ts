import { t } from '@/lib/i18n'

// Dates are calendar dates (YYYY-MM-DD) compared in the organisation time zone (§10.6, G-03).
let orgToday = new Date().toISOString().slice(0, 10)
let dateFormat = 'yyyy-MM-dd'
export function setDateContext(today: string, fmt: string) { orgToday = today; dateFormat = fmt }
export const today = () => orgToday

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']

export function fmtDate(d: string | null | undefined): string {
  if (!d) return t('common.dash')
  // Timestamps are shown as the viewer's local date; calendar dates are shown as stored.
  if (d.length > 10 && d.includes('T')) d = localIso(new Date(d))
  const [y, m, day] = d.slice(0, 10).split('-')
  if (dateFormat === 'd MMM yyyy') return `${Number(day)} ${MONTHS[Number(m) - 1]} ${y}`
  return `${y}-${m}-${day}`
}

/** "Sep 2026", for month labels on time axes. */
export function monthLabel(d: string): string {
  const [y, m] = d.slice(0, 10).split('-')
  return `${MONTHS[Number(m) - 1]} ${y}`
}

export function shortDate(d: string | null | undefined): string {
  if (!d) return t('common.dash')
  const [, m, day] = d.slice(0, 10).split('-')
  return `${MONTHS[Number(m) - 1]} ${Number(day)}`
}

export function daysBetween(a: string, b: string): number {
  return Math.round((Date.parse(b.slice(0, 10) + 'T00:00:00Z') - Date.parse(a.slice(0, 10) + 'T00:00:00Z')) / 86_400_000)
}

export function addDays(d: string, n: number): string {
  const x = new Date(d.slice(0, 10) + 'T00:00:00Z')
  x.setUTCDate(x.getUTCDate() + n)
  return x.toISOString().slice(0, 10)
}

/** "in 4 days", "3 days ago", "today" relative to the organisation's today. */
export function relative(d: string | null | undefined): string {
  if (!d) return ''
  const n = daysBetween(orgToday, d)
  if (n === 0) return t('rel.today')
  if (n === 1) return t('rel.tomorrow')
  if (n === -1) return t('rel.yesterday')
  return n > 0 ? t('rel.inDays', { n }) : t('rel.daysAgo', { n: -n })
}

/** Timestamps show in the viewer's local time with the full value available on hover (§13.0). */
export function fmtTime(ts: string | null | undefined): string {
  if (!ts) return t('common.dash')
  const d = new Date(ts)
  return `${fmtDate(localIso(d))} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}

export function ago(ts: string | null | undefined): string {
  if (!ts) return ''
  const s = (Date.now() - Date.parse(ts)) / 1000
  if (s < 60) return t('rel.justNow')
  if (s < 3600) return t('rel.minutesAgo', { n: Math.floor(s / 60) })
  if (s < 86400) return t('rel.hoursAgo', { n: Math.floor(s / 3600) })
  return t('rel.daysAgo', { n: Math.floor(s / 86400) })
}

export function localIso(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

export const hours = (h: number | null | undefined) => (h == null ? t('common.dash') : `${Number(h).toFixed(2).replace(/\.?0+$/, '')} h`)
