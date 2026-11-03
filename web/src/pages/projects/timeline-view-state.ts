/** Saved definitions describe the existing Timeline layout, not a snapshot of schedule data. */
export type TimelineViewKind = 'project' | 'workspace'
export type TimelineZoom = 'week' | 'month' | 'quarter'
export const TIMELINE_COMMON_LAYERS = ['dependencies', 'milestones', 'originalDates', 'baselineDates'] as const
export type TimelineLayer = (typeof TIMELINE_COMMON_LAYERS)[number] | 'handoffs'
export const PROJECT_TIMELINE_FILTERS = ['disciplineId', 'milestoneId', 'status', 'hideCompleted', 'from', 'to'] as const
export const WORKSPACE_TIMELINE_FILTERS = ['hideCompleted'] as const

export function timelineViewZoom(value: string | null): TimelineZoom {
  return value === 'week' || value === 'quarter' ? value : 'month'
}

export function timelineLayerChoices(kind: TimelineViewKind, handoffs = false, baseline = true): TimelineLayer[] {
  return [...TIMELINE_COMMON_LAYERS.filter(layer => layer !== 'baselineDates' || baseline), ...(kind === 'project' && handoffs ? ['handoffs' as const] : [])]
}

export function timelineViewLayers(value: string | null, choices: readonly TimelineLayer[]): Set<TimelineLayer> {
  const requested = value === null ? choices : value.split(',')
  return new Set(choices.filter(layer => requested.includes(layer)))
}

export function timelineViewDate(value: string | null): string | null {
  if (!value || !/^\d{4}-\d{2}-\d{2}$/.test(value) || value.startsWith('0000-')) return null
  const parsed = Date.parse(`${value}T12:00:00Z`)
  return Number.isFinite(parsed) && new Date(parsed).toISOString().slice(0, 10) === value ? value : null
}

/** Explicit none is needed so a saved definition cannot inherit another view's local expansion. */
export function timelineViewSet(value: string | null): Set<string> {
  return new Set(value === 'all' ? ['*'] : value == null || value === 'none' ? [] : value.split(',').filter(Boolean))
}

export function timelineViewSetParam(value: Set<string>): string {
  return value.has('*') ? 'all' : [...value].sort().join(',') || 'none'
}

export interface TimelineViewCapture {
  zoom: TimelineZoom
  layers: readonly string[]
  collapsed: Set<string>
  expanded?: Set<string>
  start?: string
  scope?: Record<string, string>
  chainEnabled?: boolean
}

/** Whitelist actual view state; panel/section/view/page/drag/preview state never enters a saved definition. */
export function captureTimelineView(sp: URLSearchParams, kind: TimelineViewKind, current: TimelineViewCapture): Record<string, string> {
  const filters = kind === 'project' ? PROJECT_TIMELINE_FILTERS : WORKSPACE_TIMELINE_FILTERS
  const params: Record<string, string> = {}
  for (const key of filters) { const value = sp.get(key); if (value) params[key] = value }
  params.hideCompleted = sp.get('hideCompleted') === 'true' ? 'true' : 'false'
  params.zoom = current.zoom
  params.group = kind === 'project' ? 'discipline' : 'project'
  params.cols = 'items,dates'
  params.layers = [...new Set(current.layers)].sort().join(',') || 'none'
  params.collapsed = timelineViewSetParam(current.collapsed)
  if (kind === 'project') {
    params.expanded = timelineViewSetParam(current.expanded ?? new Set<string>())
    if (current.chainEnabled && sp.get('chainTo')) params.chainTo = sp.get('chainTo')!
  } else {
    if (current.start) params.start = current.start
    for (const key of ['projects', 'ws']) if (current.scope?.[key]) params[key] = current.scope[key]
    if (!params.ws && !params.projects) params.projects = 'mine'
  }
  return params
}
