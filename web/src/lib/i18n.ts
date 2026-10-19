import type { GeneratedItemTypeKey, GeneratedFieldKey, VocabularyValue } from '@/gen/registry.gen'
import { en } from '@/i18n/en'

// All interface text lives in src/i18n/en.ts (§22, Q7); a second language adds a sibling table.
type Dict = Record<string, string>
const table: Dict = en

export type TextKey = keyof typeof en | GeneratedItemTypeKey | GeneratedFieldKey | `value.${VocabularyValue}`

export function t(key: TextKey, params?: Record<string, string | number | null | undefined>): string {
  let s = table[key] ?? key
  if (params) for (const [k, v] of Object.entries(params)) s = s.replaceAll(`{${k}}`, v == null ? '' : String(v))
  return s
}

/** Explicit escape hatch for keys assembled from server data or reusable component props (§44.3). */
export const runtimeText = (key: string, params?: Record<string, string | number | null | undefined>) => t(key as TextKey, params)

/** Canonical names are stored verbatim (§10); translation goes through the `value.` table. */
export const tv = (value: string | null | undefined) => (value == null ? '—' : table[`value.${value}`] ?? value)

export function plural(n: number, one: string, other: string, params: Record<string, string | number> = {}) {
  return runtimeText(n === 1 ? one : other, { n, ...params })
}
