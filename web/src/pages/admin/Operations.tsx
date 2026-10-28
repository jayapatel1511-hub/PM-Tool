import { useMutation, useQuery } from '@tanstack/react-query'
import { Empty, ErrorBanner, Loading, Missing, Section, tdCls, thCls } from '@/components/hub/common'
import { Pill } from '@/components/hub/pills'
import { typedRequest } from '@/lib/api'
import { ago, fmtTime } from '@/lib/format'
import { t , runtimeText } from '@/lib/i18n'
import { cn } from '@/lib/utils'

/** Operations (§23.8, packet 011 FR-006): the conditions operators are alerted on, checked now, and each background job's
 *  last run. The same checks run every five minutes and raise the environment's alert. */
/** Jobs are listed by their label; an unknown job keeps its identifier. */
const jobLabel = (job: string) => { const k = `ops.job.${job}`; const s = runtimeText(k); return s === k ? job : s }

export function Operations() {
  const q = useQuery({ queryKey: ['admin', 'operations'], queryFn: () => typedRequest('get', '/api/v1/admin/operations', {}), refetchInterval: 60_000 })
  const retry = useMutation({ mutationFn: (id?: string) => typedRequest('post', '/api/v1/admin/operations/outbox/retry', { query: { id }, body: {} }), onSuccess: () => { void q.refetch() } })
  if (q.isPending) return <div className="rounded-lg border bg-card"><Loading rows={6} /></div>
  if (q.error) return <ErrorBanner error={q.error} retry={() => q.refetch()} />
  const d = q.data
  return (
    <div className="space-y-6">
      <Section title={t('ops.checks')} actions={<span className="text-xs/[18px] text-muted-foreground tabular-nums">{t('ops.asOf', { time: fmtTime(d.asOf) })}</span>}>
        {d.problems.length === 0 ? <div className="px-5 py-4 text-sm"><Pill tone="ok">{t('ops.clear')}</Pill></div> : (
          <ul className="divide-y text-sm">
            {d.problems.map((p, i) => <li key={i} className="flex flex-wrap items-center gap-x-3 gap-y-1 px-5 py-3"><Pill tone="bad">{runtimeText(`ops.kind.${p.kind}`)}</Pill><span className="min-w-0 break-words">{p.detail}</span></li>)}
          </ul>
        )}
        <p className="border-t px-5 py-3 text-xs/[18px] text-muted-foreground">{t('ops.pending', { n: d.pendingChanges })} {t('ops.alertNote')}</p>
      </Section>
      <Section title={t('ops.jobs')} count={d.jobs.length}>
        {d.jobs.length === 0 ? <Empty>{t('ops.noJobs')}</Empty> : (
          <div className="scroll-region overflow-x-auto">
            <table className="w-full text-sm">
              <caption className="sr-only">{t('ops.jobs')}</caption>
              <thead className="bg-muted">
                <tr>{['job', 'lastRun', 'status', 'lastSuccess', 'failed24h'].map((c) => <th key={c} scope="col" className={cn(thCls, c === 'failed24h' && 'text-right')}>{runtimeText(`ops.col.${c}`)}</th>)}</tr>
              </thead>
              <tbody>
                {d.jobs.map((j) => (
                  <tr key={j.job} className="border-t hover:bg-muted">
                    <td className={tdCls} title={j.job}>{jobLabel(j.job)}</td>
                    <td className={cn(tdCls, 'whitespace-nowrap')} title={fmtTime(j.startedAt)}>{ago(j.startedAt)}</td>
                    <td className={tdCls}><Pill tone={j.status === 'Succeeded' ? 'ok' : j.status === 'Failed' ? 'bad' : 'work'}>{runtimeText(`ops.status.${j.status}`)}</Pill></td>
                    <td className={cn(tdCls, 'whitespace-nowrap')} title={j.lastSuccess ? fmtTime(j.lastSuccess) : undefined}>{j.lastSuccess ? ago(j.lastSuccess) : <Missing />}</td>
                    <td className={cn(tdCls, 'text-right tabular-nums', j.failed24h > 0 && 'font-semibold text-bad')}>{j.failed24h}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Section>
      {retry.error && <ErrorBanner error={retry.error} />}
      {(d.deadLetters?.length ?? 0) > 0 && <Section title={t('ops.deadLetterCount', { n: d.deadLetterCount })} actions={<button className="text-sm text-primary hover:underline" disabled={retry.isPending} onClick={() => retry.mutate(undefined)}>{t('ops.retryAll')}</button>}>
        <ul className="divide-y text-sm">{d.deadLetters.map((e) => <li key={e.id} className="flex flex-wrap items-center justify-between gap-3 px-5 py-3"><span><strong>{e.eventType}</strong> {e.projectNumber} <span className="text-muted-foreground">{fmtTime(e.createdAt)} · {t('ops.attempts', { n: e.attempts })} · {fmtTime(e.deadLetteredAt)} · {e.lastError}</span></span><button className="text-primary hover:underline" disabled={retry.isPending} onClick={() => retry.mutate(e.id)}>{t('ops.retry')}</button></li>)}</ul>
      </Section>}
    </div>
  )
}
