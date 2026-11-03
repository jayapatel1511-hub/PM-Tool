import { useQuery } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router'
import type { components } from '@/gen/api'
import { useFlag, useMe } from '@/lib/auth'
import { typedRequest } from '@/lib/api'
import { fmtDate } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import { ErrorBanner, Loading } from '@/components/hub/common'
import { RegisterPager } from './CoordinationForms'
import { useWorkRecordOrigin } from './work-record-navigation'

type Agenda = components['schemas']['WeeklyReadinessAgenda']
export function useWeeklyReadinessAgenda(projectId: string, period?: { from: string; to: string }, disciplineId?: string, parentAvailable = true) {
  const enabled = useFlag('register_screens'), me = useMe(), [sp, setSp] = useSearchParams()
  const constraintsPage = Math.max(1, Math.floor(Number(sp.get('readinessConstraintsPage')) || 1))
  const proposalsPage = Math.max(1, Math.floor(Number(sp.get('readinessProposalsPage')) || 1))
  const query = useQuery({ queryKey: ['weekly-readiness-agenda', me.id, projectId, enabled, period?.from, period?.to, disciplineId, constraintsPage, proposalsPage],
    enabled: enabled && parentAvailable && !!period,
    queryFn: () => typedRequest('get', '/api/v1/projects/{projectId}/coordination/readiness-agenda', { params: { projectId },
      query: { from: period!.from, to: period!.to, disciplineId, constraintsPage, proposalsPage, pageSize: 50 } }) })
  const setPage = (key: 'readinessConstraintsPage' | 'readinessProposalsPage', value: number) => setSp(previous => {
    const next = new URLSearchParams(previous); if (value > 1) next.set(key, String(value)); else next.delete(key); return next
  }, { replace: true })
  // Never use previous-key data or background-error data as current meeting facts.
  return { enabled, query, data: enabled && parentAvailable && !query.isError ? query.data : undefined,
    setConstraintsPage: (value: number) => setPage('readinessConstraintsPage', value), setProposalsPage: (value: number) => setPage('readinessProposalsPage', value) }
}
type Model = ReturnType<typeof useWeeklyReadinessAgenda>
const disclosure = (page: Agenda['constraints'] | Agenda['proposals']) => t('s38.pageDisclosure', {
  shown: page.items.length, total: page.totalCount, page: page.page, pages: Math.max(1, Math.ceil(page.totalCount / page.pageSize)),
})

export function weeklyReadinessSummary(model: Model) {
  if (!model.enabled) return ''
  const data = model.data
  if (!data) return t(model.query.isError ? 's38.agendaUnavailable' : 's38.agendaLoading')
  return [t('s38.agendaTitle'), t('s38.window', { from: fmtDate(data.from), to: fmtDate(data.to) }), t('s38.carryover', { to: fmtDate(data.to) }),
    `${t('s38.constraints')} — ${disclosure(data.constraints)}`,
    ...data.constraints.items.map(row => `${row.key} ${row.description} [${tv(row.state)}; ${t('s38.removalOwner')}: ${row.removalOwner.name ?? t('common.unavailable')}; ${t('s38.affectedOwner')}: ${row.affectedOwner.name ?? t('common.unavailable')}; ${t('s38.neededBy')}: ${fmtDate(row.neededBy)}]`),
    `${t('s38.proposals')} — ${disclosure(data.proposals)}`,
    ...data.proposals.items.map(row => `${row.key} ${row.intendedOutput} [${t('s38.performer')}: ${row.performer.name ?? t('common.unavailable')}; ${t('s38.targetDate')}: ${fmtDate(row.targetDate)}; ${t('s38.recordedWeek')}: ${fmtDate(row.weekStart)}]`),
  ].join('\n')
}

export function WeeklyReadinessAgendaView({ model, projectNumber }: { model: Model; projectNumber: string }) {
  const origin = useWorkRecordOrigin()
  if (!model.enabled) return null
  if (model.query.isError) return <div className="p-4"><p>{t('s38.agendaUnavailable')}</p><ErrorBanner error={model.query.error} retry={() => model.query.refetch()} /></div>
  const data = model.data
  if (!data) return <div className="p-4"><Loading rows={3} /></div>
  const path = (kind: string, id: string) => `/projects/${projectNumber}/readiness?from=${data.from}&to=${data.to}&panel=${kind}:${id}`
  return <div className="space-y-5 p-4" aria-label={t('s38.agendaTitle')}>
    <p className="font-medium">{t('s38.window', { from: fmtDate(data.from), to: fmtDate(data.to) })}</p>
    <section aria-label={t('s38.constraints')} className="space-y-2">
      <h3 className="font-semibold">{t('s38.constraints')} · {data.constraints.totalCount}</h3>
      <p className="text-xs text-muted-foreground">{t('s38.carryover', { to: fmtDate(data.to) })}</p>
      {data.constraints.items.length === 0 && <p>{t('common.empty')}</p>}
      <ul className="divide-y">{data.constraints.items.map(row => <li key={row.id} className="space-y-1 py-3">
        <Link id={`weekly-readiness-${data.projectId}-WorkConstraint-${row.id}`} className="font-medium text-primary underline underline-offset-4" onClick={origin} to={path('WorkConstraint', row.id)}>{row.key} · {row.description}</Link>
        <p className="text-xs">{tv(row.state)} · {t('s38.neededBy')}: {fmtDate(row.neededBy)}</p>
        <p className="text-xs">{t('s38.removalOwner')}: {row.removalOwner.name ?? t('common.unavailable')} ({tv(row.removalOwner.state)}) · {t('s38.affectedOwner')}: {row.affectedOwner.name ?? t('common.unavailable')} ({tv(row.affectedOwner.state)})</p>
        {row.targetState !== 'Available' && <p className="text-xs text-warn">{t('s38.targetUnavailable')}</p>}
      </li>)}</ul>
      <p className="text-xs text-muted-foreground">{disclosure(data.constraints)}</p>
      <div className="no-print"><RegisterPager label={t('s38.constraints')} page={data.constraints.page} total={data.constraints.totalCount} pageSize={data.constraints.pageSize} onPage={model.setConstraintsPage} /></div>
    </section>
    <section aria-label={t('s38.proposals')} className="space-y-2">
      <h3 className="font-semibold">{t('s38.proposals')} · {data.proposals.totalCount}</h3>
      {data.proposals.items.length === 0 && <p>{t('common.empty')}</p>}
      <ul className="divide-y">{data.proposals.items.map(row => <li key={row.id} className="space-y-1 py-3">
        <Link id={`weekly-readiness-${data.projectId}-OutputCommitment-${row.id}`} className="font-medium text-primary underline underline-offset-4" onClick={origin} to={path('OutputCommitment', row.id)}>{row.key} · {row.intendedOutput}</Link>
        <p className="text-xs">{t('s38.performer')}: {row.performer.name ?? t('common.unavailable')} ({tv(row.performer.state)})</p>
        <p className="text-xs">{t('s38.targetDate')}: {fmtDate(row.targetDate)} · {t('s38.recordedWeek')}: {fmtDate(row.weekStart)}</p>
        {row.targetState !== 'Available' && <p className="text-xs text-warn">{t('s38.targetUnavailable')}</p>}
      </li>)}</ul>
      <p className="text-xs text-muted-foreground">{disclosure(data.proposals)}</p>
      <div className="no-print"><RegisterPager label={t('s38.proposals')} page={data.proposals.page} total={data.proposals.totalCount} pageSize={data.proposals.pageSize} onPage={model.setProposalsPage} /></div>
    </section>
  </div>
}
