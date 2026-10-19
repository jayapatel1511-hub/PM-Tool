/** `view` belongs to saved views; old mode links remain valid indefinitely (FR-PLAT-09). */
export function pageMode(params: URLSearchParams, key: 'tab' | 'layout', modes: readonly string[], fallback: string) {
  const value = params.get(key) ?? params.get('view')
  return value && modes.includes(value) ? value : fallback
}

export function migratePageMode(params: URLSearchParams, key: 'tab' | 'layout', modes: readonly string[]) {
  const legacy = params.get('view')
  if (!legacy || !modes.includes(legacy)) return null
  const next = new URLSearchParams(params)
  if (!next.has(key)) next.set(key, legacy)
  next.delete('view')
  return next
}
