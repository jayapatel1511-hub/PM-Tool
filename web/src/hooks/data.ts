import { useQuery, useQueryClient } from '@tanstack/react-query'
import { typedRequest } from '@/lib/api'
import { refreshProject } from '@/lib/keys'
import type { ProjectDetail } from '@/lib/types'

/** Active and inactive reference data for pickers and labels (inactive entries only label existing items). */
export function useReference() {
  return useQuery({ queryKey: ['reference'], queryFn: () => typedRequest('get', '/api/v1/reference', {}), staleTime: 5 * 60_000 })
}

export function useProject(idOrNumber: string | undefined) {
  return useQuery({ queryKey: ['project', idOrNumber], queryFn: async () => (await typedRequest('get', '/api/v1/projects/{idOrNumber}', { params: { idOrNumber: idOrNumber! } })) as ProjectDetail, enabled: !!idOrNumber })
}

/** Invalidate everything derived from one project after a change (lists, header, derived state). */
export function useProjectRefresh() {
  const qc = useQueryClient()
  return (projectId?: string) => refreshProject(qc, projectId)
}
