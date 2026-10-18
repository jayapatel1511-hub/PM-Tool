import { useQuery } from '@tanstack/react-query'
import { get, qs } from '@/lib/api'
import { useFlag } from '@/lib/auth'

export type OrgUnit = { id: string; parentId?: string | null; name: string }
export type Placement = { id: string; unitId?: string | null }
type Organisation = { units?: OrgUnit[]; people?: { id: string; unitId?: string | null }[] }

/** Placement is a separate, scoped read so existing people/workload payloads stay unchanged. */
export function useOrgPlacement(ids: string[]) {
  const enabled = useFlag('org_structure')
  const unique = [...new Set(ids)].sort()
  const org = useQuery({ queryKey: ['organisation'], enabled, queryFn: () => get<Organisation>('organisation') })
  const placements = useQuery({ queryKey: ['organisation-placements', unique], enabled: enabled && unique.length > 0, queryFn: () => get<Placement[]>(`organisation/placements${qs({ ids: unique.join(',') })}`) })
  const units = org.data?.units ?? []
  const placementMap = new Map<string, string | null>([...(org.data?.people ?? []).map(p => [p.id, p.unitId ?? null] as const), ...(placements.data ?? []).map(p => [p.id, p.unitId ?? null] as const)])
  return { enabled, units, placementMap, loading: org.isPending || placements.isPending, error: org.error ?? placements.error }
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
