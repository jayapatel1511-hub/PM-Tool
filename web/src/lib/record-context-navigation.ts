/** A context link remembers the exact inspected record and its prior return chain. */
export type RecordContextSurfaceSlot = 'dialog-body' | 'dialog-content' | 'sheet-content'
export type RecordContextSurface = { slot: RecordContextSurfaceSlot; scrollTop: number; scrollLeft: number }
export type RecordContextDisclosure = { id: string; open: boolean }
export type RecordContextOrigin = { url: string; scrollTop: number; scrollLeft: number; surface?: RecordContextSurface; focusId?: string; disclosures?: RecordContextDisclosure[]; parent?: unknown }
type Location = { pathname: string; search: string; hash: string; state: unknown }
const surfaceSlots: RecordContextSurfaceSlot[] = ['dialog-body', 'dialog-content', 'sheet-content']
const isSurfaceSlot = (value: unknown): value is RecordContextSurfaceSlot => typeof value === 'string' && surfaceSlots.includes(value as RecordContextSurfaceSlot)
const safeElementId = (value: unknown): value is string => typeof value === 'string' && value.length > 0 && value.length <= 200 && !/[\u0000-\u001f\u007f]/.test(value)
export function recordContextDisclosures(value: unknown): RecordContextDisclosure[] {
  if (!Array.isArray(value)) return []
  const ids = new Set<string>(), result: RecordContextDisclosure[] = []
  for (const item of value.slice(0, 256)) {
    if (!item || typeof item !== 'object' || !safeElementId(item.id) || typeof item.open !== 'boolean' || ids.has(item.id)) continue
    ids.add(item.id); result.push({ id: item.id, open: item.open })
  }
  return result
}
const visible = (element: Element) => {
  if (!(element instanceof HTMLElement)) return false
  if (element.closest('[data-state="closed"], [aria-hidden="true"], [hidden]')) return false
  const style = window.getComputedStyle(element)
  const rect = element.getBoundingClientRect()
  return style.display !== 'none' && style.visibility !== 'hidden' && rect.width > 0 && rect.height > 0
}
const surfaceElement = (source?: Element | null): HTMLElement | null => {
  if (source) {
    for (const slot of surfaceSlots) {
      const found = source.closest<HTMLElement>(`[data-slot="${slot}"]`)
      if (found && visible(found)) return found
    }
  }
  if (!source) {
    const active = document.activeElement
    if (active instanceof Element) {
      for (const slot of surfaceSlots) {
        const found = active.closest<HTMLElement>(`[data-slot="${slot}"]`)
        if (found && visible(found)) return found
      }
    }
  }
  for (const slot of surfaceSlots) {
    const surfaces = Array.from(document.querySelectorAll<HTMLElement>(`[data-slot="${slot}"]`))
    const found = surfaces.reverse().find(visible)
    if (found) return found
  }
  return null
}

export function findRecordContextSurface(slot: RecordContextSurfaceSlot) {
  return Array.from(document.querySelectorAll<HTMLElement>(`[data-slot="${slot}"]`)).reverse().find(visible) ?? null
}

export function captureRecordContextOrigin(location: Location, source?: Element | null): RecordContextOrigin {
  const content = document.getElementById('content')
  const state = location.state as { recordContextReturn?: unknown } | null
  const surface = surfaceElement(source)
  const disclosureScope = surface ?? content
  const disclosures = recordContextDisclosures(Array.from(disclosureScope?.querySelectorAll<HTMLDetailsElement>('details[data-person-source-disclosure][id]') ?? [])
    .map(element => ({ id: element.id, open: element.open })))
  return { url: `${location.pathname}${location.search}${location.hash}`, scrollTop: content?.scrollTop ?? 0,
    scrollLeft: content?.scrollLeft ?? 0, ...(surface ? { surface: { slot: surface.dataset.slot as RecordContextSurfaceSlot, scrollTop: surface.scrollTop, scrollLeft: surface.scrollLeft } } : {}),
    ...(source instanceof HTMLElement && source.id ? { focusId: source.id } : {}), ...(disclosures.length ? { disclosures } : {}), parent: state?.recordContextReturn }
}

/** Accept only application-local destinations, including older string return targets. */
export function recordContextReturn(state: unknown) {
  const value = (state as { recordContextReturn?: unknown } | null)?.recordContextReturn
  const safe = (url: unknown): url is string => typeof url === 'string' && url.startsWith('/') && !url.startsWith('//') && !url.includes('\\')
  if (typeof value === 'string') return safe(value) ? { url: value, state: null } : null
  if (!value || typeof value !== 'object' || !('url' in value) || !safe(value.url)) return null
  const r = value as Partial<RecordContextOrigin> & { url: string }
  const position = (n: unknown) => typeof n === 'number' && Number.isFinite(n) ? Math.max(0, n) : 0
  const surface = r.surface && typeof r.surface === 'object' && isSurfaceSlot(r.surface.slot)
    ? { slot: r.surface.slot, scrollTop: position(r.surface.scrollTop), scrollLeft: position(r.surface.scrollLeft) }
    : undefined
  const focusId = safeElementId(r.focusId) ? r.focusId : undefined
  const disclosures = recordContextDisclosures(r.disclosures)
  return { url: r.url, state: { recordContextReturn: r.parent ?? null,
    recordContextRestore: { scrollTop: position(r.scrollTop), scrollLeft: position(r.scrollLeft), ...(surface ? { surface } : {}), ...(focusId ? { focusId } : {}), ...(disclosures.length ? { disclosures } : {}) } } }
}
