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

/** The names under one key prefix, so a screen's `label('name')` helper stays checked against en.ts. */
export type KeyIn<P extends string, K = keyof typeof en> = K extends `${P}${infer S}` ? S : never
/** A label for a value the server or address bar sends, read through a typed key map; a value the map does not name shows as sent. */
export const textFor = (keys: Readonly<Record<string, TextKey>>, value: string) => Object.hasOwn(keys, value) ? t(keys[value]) : value
/** For a set en.ts itself defines (event types, template fields): true when `prefix + name` has text, so `${prefix}${name}` is a checked key. */
export const hasKey = <P extends string>(prefix: P, name: string): name is KeyIn<P> => Object.hasOwn(en, prefix + name)

/** Explicit escape hatch for keys assembled from server data or reusable component props (§44.3). */
export const runtimeText = (key: string, params?: Record<string, string | number | null | undefined>) => t(key as TextKey, params)
/** An organisation setting's name as the Settings page shows it; a flag without its packet shows its name alone. */
export const settingLabel = (key: string, packet?: string | null) => key.startsWith('rule_enabled.') ? t('rule.enabled', { rule: key.slice(13), name: runtimeText(`rule.${key.slice(13)}`) })
  : key.startsWith('notify_default.') ? runtimeText(`event.${key.slice(15)}`)
  : key.startsWith('flag.') ? (packet ? t('admin.flagLabel', { name: key.slice(5), packet }) : key.slice(5)) : runtimeText(`setting.${key}`)

/** Canonical names are stored verbatim (§10); translation goes through the `value.` table. */
export const tv = (value: string | null | undefined) => (value == null ? '—' : table[`value.${value}`] ?? value)

export function plural(n: number, one: string, other: string, params: Record<string, string | number> = {}) {
  return runtimeText(n === 1 ? one : other, { n, ...params })
}
