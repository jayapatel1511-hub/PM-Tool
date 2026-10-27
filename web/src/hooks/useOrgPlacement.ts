import { useQuery } from '@tanstack/react-query'
import { typedRequest } from '@/lib/api'
import { useFlag } from '@/lib/auth'

export type OrgUnit = { id: string; parentId?: string | null; name: string }
export type Placement = { id: string; unitId?: string | null }

/** Placement is a separate, scoped read so existing people/workload payloads stay unchanged. */
export function useOrgPlacement(ids: string[]) {
  const enabled = useFlag('org_structure')
  const unique = [...new Set(ids)].sort()
  const org = useQuery({ queryKey: ['organisation'], enabled, queryFn: () => typedRequest('get', '/api/v1/organisation', {}) })
  const placements = useQuery({ queryKey: ['organisation-placements', unique], enabled: enabled && unique.length > 0, queryFn: () => typedRequest('get', '/api/v1/organisation/placements', { query: { ids: unique.join(',') } }) })
  const units = org.data?.units ?? []
  const placementMap = new Map<string, string | null>([...(org.data?.people ?? []).map(p => [p.id, p.unitId ?? null] as const), ...(placements.data ?? []).map(p => [p.id, p.unitId ?? null] as const)])
  return { enabled, units, placementMap, loading: org.isPending || placements.isPending, error: org.error ?? placements.error }
}

/** A unit's name, or its path when another unit shares the name: names are unique only under one parent, so groups and
 *  filters keyed by a bare name would merge two units (FR-UNT-13). */
export function unitLabel(units: OrgUnit[], id: string) {
  const unit = units.find(u => u.id === id)
  if (!unit || units.filter(u => u.name === unit.name).length < 2) return unit?.name
  const path: OrgUnit[] = []
  for (let u: OrgUnit | undefined = unit; u && !path.includes(u); u = units.find(x => x.id === u!.parentId)) path.unshift(u)
  return path.map(u => u.name).join(' / ')
}

/** Descendants use a visited set so malformed hierarchy cycles cannot hang the UI. */
export function descendantUnitIds(units: OrgUnit[], rootId: string) {
  const found = new Set<string>([rootId]);
  let changed = true
  while (changed) {
    changed = false
    for (const unit of units) if (unit.parentId && found.has(unit.parentId) && !found.has(unit.id)) { found.add(unit.id); changed = true }
  }
  return found
}
