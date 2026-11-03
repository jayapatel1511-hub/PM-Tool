import { useQuery } from '@tanstack/react-query'
import { Link, useLocation, useSearchParams } from 'react-router'
import type { components } from '@/gen/api'
import { useFlag, useMe } from '@/lib/auth'
import { typedRequest } from '@/lib/api'
import { fmtDate } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import { ErrorBanner, Loading } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { RegisterPager } from './CoordinationForms'
import { useWorkRecordOrigin } from './work-record-navigation'

type Links = components['schemas']['HandoffReadinessLinks']
export function HandoffReadinessSummary({ projectId, projectNumber, handoffId, links, surface = 'panel' }: {
  projectId: string; projectNumber: string; handoffId: string; links?: Links | null; surface?: string
}) {
  const enabled = useFlag('register_screens'), me = useMe(), origin = useWorkRecordOrigin()
  const [sp, setSp] = useSearchParams(), location = useLocation(), prefix = `${surface}:${handoffId}:`
  const saved = sp.get('handoffReadiness'), expanded = saved?.startsWith(prefix) === true
  const parsed = expanded ? Number(saved?.slice(prefix.length)) : 1
  const page = Number.isSafeInteger(parsed) && parsed > 0 ? parsed : 1
  const setPage = (next: number | null) => setSp(previous => { const params = new URLSearchParams(previous)
    if (next === null) params.delete('handoffReadiness'); else params.set('handoffReadiness', `${prefix}${next}`)
    return params
  }, { replace: true, state: location.state })
  const q = useQuery({ queryKey: ['handoff-readiness-links', me.id, projectId, handoffId, enabled, page], enabled: enabled && expanded,
    queryFn: () => typedRequest('get', '/api/v1/projects/{projectId}/handoffs/{id}/readiness-linked-tasks', { params: { projectId, id: handoffId }, query: { page, pageSize: 50 } }) })
  if (!enabled) return null
  // An expanded current error never falls back to cached summary/rows.
  const summary = expanded ? q.isError ? undefined : q.data?.summary : links
  const tasks = expanded && !q.isError ? q.data?.tasks : undefined
  const known = summary?.state === 'Known'
  return <div role="region" className="w-full space-y-2 rounded-md border p-3 text-sm" aria-label={t('s38.linksTitle')}>
    <p className="font-medium">{known ? t('s38.count', { n: summary.taskCount ?? 0 }) : expanded && q.isPending ? t('s38.agendaLoading') : t('s38.unavailable')}</p>
    <p className="text-xs text-muted-foreground">{t('s38.projectScope')}</p>
    {known && <>
      <p className="text-xs">{summary.taskCount === 0 ? t('s38.noLinks') : summary.firstRecordedDueDate
        ? t('s38.firstDue', { date: fmtDate(summary.firstRecordedDueDate), dated: summary.datedTaskCount ?? 0, total: summary.taskCount ?? 0 }) : t('s38.noDates')}</p>
      <p className="text-xs text-muted-foreground">{t('s38.overlap', { direct: summary.directTaskCount ?? 0, linked: summary.constraintLinkedTaskCount ?? 0 })}</p>
      {summary.isHistoricalMismatch && <p className="text-xs text-warn">{t('s38.historical')}</p>}
      {!summary.isHistoricalMismatch && summary.hasPromisedDateMismatch && <p className="text-xs text-warn">{t('handoff.dateMismatch')}</p>}
      {summary.isOverdue && <p className="text-xs text-bad">{t('handoff.overdue')}</p>}
      {summary.directReceiptSatisfied === true && <p className="text-xs">{t('s38.receiptSatisfied')}</p>}
    </>}
    <Button type="button" variant="outline" size="sm" aria-expanded={expanded} onClick={() => setPage(expanded ? null : 1)}>{t(expanded ? 's38.hideTasks' : 's38.showTasks')}</Button>
    {expanded && <>
      {q.isPending ? <Loading rows={2} /> : q.isError ? <ErrorBanner error={q.error} retry={() => q.refetch()} /> : tasks && known && <>
        <ul className="space-y-3">{tasks.items.map(task => <li key={task.id}>
          <Link id={`handoff-readiness-${surface}-${handoffId}-task-${task.id}`} onClick={origin} className="text-primary underline underline-offset-4" to={`/projects/${projectNumber}/tasks?panel=Task:${task.id}`}>{task.key} · {task.name}</Link>
          <span className="tabular-nums"> · {fmtDate(task.dueDate)}</span>
          <ul className="ml-3 space-y-1 text-xs">{task.relationships.map((relationship, index) => <li key={relationship.constraintId ?? `direct:${index}`}>
            {relationship.relationship === 'DirectTarget' ? t('workInputs.directTarget') : <>
              <Link id={`handoff-readiness-${surface}-${handoffId}-task-${task.id}-constraint-${relationship.constraintId}`} onClick={origin} className="text-primary underline underline-offset-4" to={`/projects/${projectNumber}/readiness?panel=WorkConstraint:${relationship.constraintId}`}>{t('workInputs.viaConstraintKey', { key: relationship.constraintKey ?? t('common.unavailable') })}</Link>
              {' · '}{tv(relationship.constraintState ?? '')} · {t('s38.neededBy')}: {fmtDate(relationship.constraintNeededBy)}
              <p>{t('s38.removalOwner')}: {relationship.removalOwner?.name ?? t('common.unavailable')} · {t('s38.affectedOwner')}: {relationship.affectedOwner?.name ?? t('common.unavailable')}</p>
            </>}
          </li>)}</ul>
        </li>)}</ul>
        <p className="text-xs text-muted-foreground">{t('s38.pageDisclosure', { shown: tasks.items.length, total: tasks.totalCount, page: tasks.page, pages: Math.max(1, Math.ceil(tasks.totalCount / tasks.pageSize)) })}</p>
        <RegisterPager label={t('s38.taskPager')} page={tasks.page} total={tasks.totalCount} pageSize={tasks.pageSize} onPage={setPage} />
      </>}
    </>}
  </div>
}
