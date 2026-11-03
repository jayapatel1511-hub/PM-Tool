/** FR-SCR-79: local, viewer-partitioned search terms only. No request, telemetry or module-load storage access. */
export const RECENT_SEARCH_LIMIT = 10
export const RECENT_SEARCH_MAX_LENGTH = 200
const MAX_STORED_LENGTH = 16_384
const illegalText = /[\u0000-\u001f\u007f-\u009f\u2028\u2029]|[\uD800-\uDFFF]/u

export interface RecentSearchStorage {
  getItem(key: string): string | null
  setItem(key: string, value: string): void
  removeItem(key: string): void
}
export type RecentSearchStorageSource = () => RecentSearchStorage | null
export type RecentSearchVisit = { viewerId: string; term: string }
const browserStorage: RecentSearchStorageSource = () => typeof window === 'undefined' ? null : window.localStorage

/** Preserve search meaning: trim outside whitespace, keep internal spacing/case, and reject rather than truncate. */
export function normalizeRecentSearch(value: unknown): string | null {
  if (typeof value !== 'string' || illegalText.test(value)) return null
  const term = value.trim()
  return term.length > 0 && term.length <= RECENT_SEARCH_MAX_LENGTH ? term : null
}

export function recentSearchStorageKey(viewerId: unknown): string | null {
  if (typeof viewerId !== 'string' || !viewerId || viewerId.length > 128 || viewerId !== viewerId.trim() || illegalText.test(viewerId)) return null
  return `hub.recent-searches.v1.${encodeURIComponent(viewerId)}`
}

function entries(value: unknown): string[] {
  if (!Array.isArray(value)) return []
  const result: string[] = [], found = new Set<string>()
  for (const candidate of value.slice(0, 100)) {
    const term = normalizeRecentSearch(candidate), key = term?.toLowerCase()
    if (!term || !key || found.has(key)) continue
    result.push(term); found.add(key)
    if (result.length === RECENT_SEARCH_LIMIT) break
  }
  return result
}

function load(storage: RecentSearchStorage, key: string): { items: string[]; readable: boolean } {
  let raw: string | null
  try { raw = storage.getItem(key) } catch { return { items: [], readable: false } }
  if (raw === null || typeof raw !== 'string' || raw.length > MAX_STORED_LENGTH) return { items: [], readable: true }
  try { return { items: entries(JSON.parse(raw)), readable: true } } catch { return { items: [], readable: true } }
}

export function readRecentSearches(viewerId: string, enabled: boolean, source: RecentSearchStorageSource = browserStorage): string[] {
  if (!enabled) return []
  const key = recentSearchStorageKey(viewerId)
  if (!key) return []
  try { const storage = source(); return storage ? load(storage, key).items : [] } catch { return [] }
}

/** A repeated most-recent term is idempotent; denied reads never overwrite an unknown existing history. */
export function rememberRecentSearch(viewerId: string, value: unknown, enabled: boolean, source: RecentSearchStorageSource = browserStorage): string[] {
  if (!enabled) return []
  const key = recentSearchStorageKey(viewerId)
  if (!key) return []
  try {
    const storage = source()
    if (!storage) return []
    const before = load(storage, key), term = normalizeRecentSearch(value)
    if (!before.readable || !term) return before.items
    const next = [term, ...before.items.filter(item => item.toLowerCase() !== term.toLowerCase())].slice(0, RECENT_SEARCH_LIMIT)
    if (JSON.stringify(next) === JSON.stringify(before.items)) return before.items
    try { storage.setItem(key, JSON.stringify(next)); return next } catch { return before.items }
  } catch { return [] }
}

/** False means storage refused the clear; the caller must not announce successful removal. */
export function clearRecentSearches(viewerId: string, enabled: boolean, source: RecentSearchStorageSource = browserStorage): boolean {
  if (!enabled) return false
  const key = recentSearchStorageKey(viewerId)
  if (!key) return false
  try { const storage = source(); if (!storage) return false; storage.removeItem(key); return true } catch { return false }
}

/** Only committed q changes count. Type/filter/page changes have the same term; overlay/restore visits are excluded. */
export function isRecentSearchNavigation(previous: RecentSearchVisit | null, current: RecentSearchVisit, context: { enabled: boolean; personPanel: boolean; restoring: boolean }): boolean {
  if (!context.enabled || context.personPanel || context.restoring || !recentSearchStorageKey(current.viewerId)) return false
  const term = normalizeRecentSearch(current.term)
  return term !== null && (previous === null || previous.viewerId !== current.viewerId || normalizeRecentSearch(previous.term) !== term)
}
