import { en } from '@/i18n/en'

// All interface text lives in src/i18n/en.ts (§22, Q7); a second language adds a sibling table.
type Dict = Record<string, string>
const table: Dict = en

export function t(key: string, params?: Record<string, string | number | null | undefined>): string {
  let s = table[key] ?? key
  if (params) for (const [k, v] of Object.entries(params)) s = s.replaceAll(`{${k}}`, v == null ? '' : String(v))
  return s
}

/** Canonical names are stored verbatim (§10); translation goes through the `value.` table. */
export const tv = (value: string | null | undefined) => (value == null ? '—' : table[`value.${value}`] ?? value)

export function plural(n: number, one: string, other: string, params: Record<string, string | number> = {}) {
  return t(n === 1 ? one : other, { n, ...params })
}
