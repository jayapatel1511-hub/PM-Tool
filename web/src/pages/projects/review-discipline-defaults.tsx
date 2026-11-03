import { useEffect, useRef, type Dispatch, type SetStateAction } from 'react'
import { useQuery } from '@tanstack/react-query'
import type { components } from '@/gen/api'
import { Button } from '@/components/ui/button'
import { ErrorBanner } from '@/components/hub/common'
import { useMe } from '@/lib/auth'
import { typedRequest } from '@/lib/api'
import { addDays, today } from '@/lib/format'
import { t } from '@/lib/i18n'

type Schemas = components['schemas']
type Assignment = Pick<Schemas['ReviewAssignmentRow'], 'projectDisciplineId' | 'reviewerId' | 'dueDate'>
export function useReviewDisciplineDefaults({ projectId, sources, enabled, manual, assignments, setAssignments }: {
  projectId: string; sources: string[]; enabled: boolean; manual: { current: boolean }; assignments: Assignment[]; setAssignments: Dispatch<SetStateAction<Assignment[]>>
}) {
  const me = useMe(), sourceIds = [...sources].sort().join(','), previous = useRef(sourceIds)
  const query = useQuery({ queryKey: ['review-default-suggestions', projectId, me.id, enabled, sourceIds], enabled: enabled && sources.length > 0,
    queryFn: () => typedRequest('get', '/api/v1/projects/{projectId}/reviews/default-disciplines', { params: { projectId }, query: { sourceRevisionIds: sourceIds } }) })
  const data = !query.isError && query.data?.sourceRevisionIds.slice().sort().join(',') === sourceIds ? query.data : undefined
  const suggestions = data?.disciplines.filter(row => row.state === 'Available' && row.projectDisciplineId) ?? []
  const newRows = () => suggestions.map(row => ({ projectDisciplineId: row.projectDisciplineId!, reviewerId: '', dueDate: addDays(today(), 7) }))
  const tooMany = !!data?.exceedsAssignmentLimit || new Set([...assignments.map(row => row.projectDisciplineId), ...suggestions.map(row => row.projectDisciplineId)]).size > 50
  useEffect(() => {
    if (previous.current !== sourceIds) { previous.current = sourceIds; if (enabled && !manual.current) setAssignments([]) }
    if (enabled && !manual.current && data) setAssignments(data.exceedsAssignmentLimit ? [] : newRows())
    // The source-keyed response is the only automatic initializer. Manual rows never reconcile through a late response.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [enabled, sourceIds, data, manual, setAssignments])
  const add = () => { manual.current = true; if (!data || tooMany) return; setAssignments(old => {
    const extra = newRows().filter(row => !old.some(existing => existing.projectDisciplineId === row.projectDisciplineId))
    return old.length + extra.length <= 50 ? [...old, ...extra] : old
  }) }
  return { enabled, query, data, sourceIds, manual: manual.current, add, tooMany,
    unresolved: enabled && sources.length > 0 && !manual.current && (!data || data.exceedsAssignmentLimit) }
}
export function ReviewDisciplineDefaultSuggestions({ state }: { state: ReturnType<typeof useReviewDisciplineDefaults> }) {
  if (!state.enabled) return null
  return <section className="space-y-2 rounded-md border p-3 text-sm" aria-label={t('reviewDefaults.title')}>
    <h3 className="font-medium">{t('reviewDefaults.title')}</h3><p className="text-xs text-muted-foreground">{t('reviewDefaults.hint')}</p>
    {!state.sourceIds ? <p>{t('reviewDefaults.chooseSources')}</p> : state.query.isError ? <><ErrorBanner error={state.query.error} retry={() => state.query.refetch()} /><p>{t('reviewDefaults.unavailable')}</p></> : !state.data ? <p>{t('reviewDefaults.loading')}</p> : <>
      {!state.data.disciplines.length && <p>{t('reviewDefaults.empty')}</p>}
      <ul className="space-y-1">{state.data.disciplines.map(row => <li key={row.disciplineId}>{row.name || '—'}{row.state !== 'Available' && <span className="ml-2 text-warn">{row.state === 'NotInProject' ? t('reviewDefaults.NotInProject') : row.state === 'InactiveDiscipline' ? t('reviewDefaults.InactiveDiscipline') : row.state === 'InactiveProjectDiscipline' ? t('reviewDefaults.InactiveProjectDiscipline') : t('reviewDefaults.InactiveType')}</span>}</li>)}</ul>
      {state.tooMany && <p role="status" className="text-warn">{t('reviewDefaults.tooMany')}</p>}
      {state.manual && <p>{t('reviewDefaults.manual')}</p>}
      <Button type="button" variant="outline" disabled={state.tooMany} onClick={state.add}>{t('reviewDefaults.add')}</Button>
    </>}
  </section>
}
