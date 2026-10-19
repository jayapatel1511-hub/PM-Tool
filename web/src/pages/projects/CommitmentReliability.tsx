import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, ExternalLink, RotateCcw } from 'lucide-react'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Empty, ErrorBanner, Loading, Section } from '@/components/hub/common'
import { typedRequest } from '@/lib/api'
import { invalidateChange } from '@/lib/keys'
import { fmtDate } from '@/lib/format'
import { t } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { useFlag } from '@/lib/auth'

export type Reliability = { met: number; committed: number; percent: number | null; band: string | null; withdrawn: number; notYetRecorded: number; awaiting: number }
export type ReliabilitySnapshot = { id: string; weekStart: string; reliability: Reliability }
export type ReliabilityMiss = { categoryId: string | null; category: string | null; count: number; commitments: string[] }
export type ReliabilityData = { snapshots: ReliabilitySnapshot[]; misses: ReliabilityMiss[]; awaiting: { id: string; key: string; rowVersion: number; canConfirm: boolean }[] }

/** Packet 048 S06: the reliability figure deliberately uses server supplied bands and counts. */
export function CommitmentReliability({ projectId, onOpenCommitment }: { projectId: string; onOpenCommitment: (id: string) => void }) {
  const enabled = useFlag('commitment_reliability')
  const qc = useQueryClient()
  const q = useQuery({ queryKey: ['commitment-reliability', projectId], enabled, queryFn: () => typedRequest('get', '/api/v1/projects/{projectId}/commitment-reliability', { params: { projectId } }) })
  const [returning, setReturning] = useState<string | null>(null)
  const [reason, setReason] = useState('')
  const confirm = useMutation({
    mutationFn: ({ id, rowVersion, returning, returnReason }: { id: string; rowVersion: number; returning: boolean; returnReason?: string }) =>
      typedRequest('post', '/api/v1/projects/{projectId}/weekly-commitments/{id}/confirm', { params: { projectId, id }, body: { requestId: crypto.randomUUID(), rowVersion, return: returning, reason: returnReason ?? null } }),
    onSuccess: () => { invalidateChange(qc, 'OutputCommitment', projectId); setReturning(null); setReason('') },
  })
  if (!enabled) return null
  if (q.isPending) return <Section title={t('reliability.title')}><Loading rows={3} /></Section>
  if (q.error) return <Section title={t('reliability.title')}><ErrorBanner error={q.error} retry={() => q.refetch()} /></Section>
  const data = q.data
  const latest = data.snapshots[0]
  return <div className="space-y-6">
    <Section title={t('reliability.title')}>
      {!latest ? <Empty>{t('reliability.empty')}</Empty> : <div className="space-y-4 px-5 py-4">
        <div className="flex flex-wrap items-baseline gap-x-4 gap-y-1">
          <p className="text-2xl font-semibold tabular-nums">{t('reliability.figure', { met: latest.reliability.met, committed: latest.reliability.committed, percent: latest.reliability.percent == null ? '—' : latest.reliability.percent })}</p>
          <span className={cn('rounded-md bg-secondary px-2 py-0.5 text-xs font-medium', latest.reliability.band?.toLowerCase() === 'low' && 'text-bad', latest.reliability.band?.toLowerCase() === 'watch' && 'text-warn')}>{latest.reliability.band}</span>
          <span className="text-xs text-muted-foreground">{fmtDate(latest.weekStart)}</span>
        </div>
        <dl className="grid gap-2 text-sm sm:grid-cols-3">
          <div><dt className="text-muted-foreground">{t('reliability.withdrawn')}</dt><dd className="tabular-nums">{latest.reliability.withdrawn}</dd></div>
          <div><dt className="text-muted-foreground">{t('reliability.notYetRecorded')}</dt><dd className="tabular-nums">{latest.reliability.notYetRecorded}</dd></div>
          <div><dt className="text-muted-foreground">{t('reliability.awaiting')}</dt><dd className="tabular-nums">{latest.reliability.awaiting}</dd></div>
        </dl>
        {data.snapshots.length > 1 && <div className="overflow-x-auto"><table className="w-full text-sm"><caption className="sr-only">{t('reliability.trend')}</caption><thead className="bg-muted"><tr><th className="px-3 py-2 text-left">{t('reliability.week')}</th><th className="px-3 py-2 text-right">{t('reliability.rate')}</th><th className="px-3 py-2 text-left">{t('reliability.band')}</th></tr></thead><tbody>{data.snapshots.map(s => <tr key={s.id} className="border-t"><td className="px-3 py-2">{fmtDate(s.weekStart)}</td><td className="px-3 py-2 text-right tabular-nums">{s.reliability.percent == null ? '—' : `${s.reliability.percent}%`}</td><td className="px-3 py-2">{s.reliability.band}</td></tr>)}</tbody></table></div>}
      </div>}
    </Section>
    <Section title={t('reliability.misses')}>
      {data.misses.length === 0 ? <Empty>{t('reliability.noMisses')}</Empty> : <ul className="divide-y">{data.misses.map(m => <li key={m.categoryId ?? m.category} className="flex flex-wrap items-center gap-3 px-5 py-3 text-sm"><span className="min-w-44 font-medium">{m.category}</span><span className="text-muted-foreground tabular-nums">{m.count}</span><div className="flex flex-wrap gap-2">{m.commitments.map(c => <Button key={c} size="sm" variant="link" className="h-auto p-0" onClick={() => onOpenCommitment(c)}>{c.slice(0, 8)} <ExternalLink className="ml-1 size-3" /></Button>)}</div></li>)}</ul>}
    </Section>
    {data.awaiting.length > 0 && <Section title={t('reliability.confirmations')} count={data.awaiting.length}>
      <ul className="divide-y">{data.awaiting.map(a => <li key={a.id} className="flex flex-wrap items-center gap-3 px-5 py-3 text-sm"><Button variant="link" className="h-auto p-0" onClick={() => onOpenCommitment(a.id)}>{a.key}</Button>{a.canConfirm ? <div className="ml-auto flex flex-wrap gap-2"><Button size="sm" onClick={() => confirm.mutate({ id: a.id, rowVersion: a.rowVersion, returning: false })} disabled={confirm.isPending}><Check className="size-4" />{t('reliability.confirm')}</Button><Button size="sm" variant="outline" onClick={() => setReturning(a.id)} disabled={confirm.isPending}><RotateCcw className="size-4" />{t('reliability.return')}</Button></div> : <span className="ml-auto text-xs text-muted-foreground">{t('reliability.performerCannotConfirm')}</span>}{returning === a.id && <form className="flex w-full gap-2" onSubmit={e => { e.preventDefault(); if (reason.trim().length >= 5) confirm.mutate({ id: a.id, rowVersion: a.rowVersion, returning: true, returnReason: reason.trim() }) }}><Input aria-label={t('reliability.returnReason')} required minLength={5} value={reason} onChange={e => setReason(e.target.value)} placeholder={t('reliability.returnReason')} /><Button size="sm" type="submit" disabled={confirm.isPending || reason.trim().length < 5}>{t('common.save')}</Button></form>}</li>)}</ul>
      {confirm.error && <ErrorBanner error={confirm.error} />}
    </Section>}
  </div>
}
