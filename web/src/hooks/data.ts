import { useQuery, useQueryClient } from '@tanstack/react-query'
import { get } from '@/lib/api'
import type { ProjectDetail, Reference } from '@/lib/types'

/** Active and inactive reference data for pickers and labels (inactive entries only label existing items). */
export function useReference() {
  return useQuery({ queryKey: ['reference'], queryFn: () => get<Reference>('reference'), staleTime: 5 * 60_000 })
}

export function useProject(idOrNumber: string | undefined) {
  return useQuery({ queryKey: ['project', idOrNumber], queryFn: () => get<ProjectDetail>(`projects/${encodeURIComponent(idOrNumber!)}`), enabled: !!idOrNumber })
}

/** Invalidate everything derived from one project after a change (lists, header, derived state). */
export function useProjectRefresh() {
  const qc = useQueryClient()
  return (projectId?: string) => {
    qc.invalidateQueries({ queryKey: ['project'] })
    qc.invalidateQueries({ queryKey: ['projects'] })
    if (projectId) qc.invalidateQueries({ queryKey: ['p', projectId] })
    qc.invalidateQueries({ queryKey: ['mywork'] })
    // Derived state is re-evaluated just after the save (§23.5); refresh once more when it lands.
    setTimeout(() => {
      qc.invalidateQueries({ queryKey: ['project'] })
      if (projectId) qc.invalidateQueries({ queryKey: ['p', projectId] })
      qc.invalidateQueries({ queryKey: ['mywork'] })
    }, 1500)
  }
}
