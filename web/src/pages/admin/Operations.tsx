import { useQuery } from '@tanstack/react-query'
import { Empty, ErrorBanner, Loading, Section } from '@/components/hub/common'
import { Pill } from '@/components/hub/pills'
import { get } from '@/lib/api'
import { ago, fmtTime } from '@/lib/format'
import { t } from '@/lib/i18n'
import { ADMIN_EXTRA } from './Admin'

interface Ops {
  asOf: string; pendingChanges: number
  problems: { kind: string; detail: string }[]
  jobs: { job: string; startedAt: string; finishedAt?: string; status: string; lastSuccess?: string; failed24h: number }[]
}

/** Operations (§23.8, packet 011 FR-006): the conditions operators are alerted on, checked now, and each background job's
 *  last run. The same checks run every five minutes and raise the environment's alert. */
export function Operations() {
  const q = useQuery({ queryKey: ['admin', 'operations'], queryFn: () => get<Ops>('admin/operations'), refetchInterval: 60_000 })
  if (q.isPending) return <Loading rows={6} />
  if (q.error) return <ErrorBanner error={q.error} retry={() => q.refetch()} />
  const d = q.data
  return (
    <div className="space-y-4">
      <Section title={t('ops.checks')} actions={<span className="text-xs text-muted-foreground">{t('ops.asOf', { time: fmtTime(d.asOf) })}</span>}>
        {d.problems.length === 0 ? <div className="px-4 py-3 text-sm"><Pill tone="ok">{t('ops.clear')}</Pill></div> : (
          <ul className="divide-y text-sm">
            {d.problems.map((p, i) => <li key={i} className="flex flex-wrap items-center gap-2 px-4 py-2"><Pill tone="bad">{t(`ops.kind.${p.kind}`)}</Pill><span>{p.detail}</span></li>)}
          </ul>
        )}
        <p className="border-t px-4 py-2 text-xs text-muted-foreground">{t('ops.pending', { n: d.pendingChanges })} {t('ops.alertNote')}</p>
      </Section>
      <Section title={t('ops.jobs')} count={d.jobs.length}>
        {d.jobs.length === 0 ? <Empty>{t('ops.noJobs')}</Empty> : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="border-b bg-muted/40 text-xs text-muted-foreground">
                <tr>{['job', 'lastRun', 'status', 'lastSuccess', 'failed24h'].map((c) => <th key={c} scope="col" className="px-3 py-2 text-left font-medium">{t(`ops.col.${c}`)}</th>)}</tr>
              </thead>
              <tbody className="divide-y">
                {d.jobs.map((j) => (
                  <tr key={j.job}>
                    <td className="px-3 py-1.5 font-mono text-xs">{j.job}</td>
                    <td className="px-3 py-1.5" title={fmtTime(j.startedAt)}>{ago(j.startedAt)}</td>
                    <td className="px-3 py-1.5"><Pill tone={j.status === 'Succeeded' ? 'ok' : j.status === 'Failed' ? 'bad' : 'work'}>{t(`ops.status.${j.status}`)}</Pill></td>
                    <td className="px-3 py-1.5">{j.lastSuccess ? ago(j.lastSuccess) : t('common.dash')}</td>
                    <td className="px-3 py-1.5 tabular-nums">{j.failed24h}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Section>
    </div>
  )
}

ADMIN_EXTRA.push({ to: '/admin/operations', label: t('ops.title') })
