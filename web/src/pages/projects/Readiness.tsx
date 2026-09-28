import { CalendarCheck, ExternalLink } from 'lucide-react'
import { useMemo } from 'react'
import { Link, useSearchParams } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import { Empty, ErrorBanner, Loading, Page, Section } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { get } from '@/lib/api'
import { addDays, fmtDate, today } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import { Chip, StatusPill } from '@/components/hub/pills'
import { useMe } from '@/lib/auth'
import { useCurrentProject } from './ProjectLayout'

type Commitment = {
  id: string; targetType: string; targetId: string; performerId: string; weekStart: string; targetDate: string
  intendedOutput: string; completionCriteria: string; state: string; snapshotId?: string | null; readinessAtCommit?: string | null
}
type Snapshot = { id: string; weekStart: string; capturedAt: string; committedCount: number; met: number; withdrawn: number }
type Weekly = { commitments: Commitment[]; total: number; truncated: boolean; snapshots: Snapshot[] }
type Constraint = { id: string; targetType: string; targetId: string; description: string; category: string; neededBy: string; sourceUrl: string }
type ReadyOutput = { id: string; targetType: string; targetId: string; key: string; name: string; dueDate: string | null; intendedOutput: string; completionCriteria: string; state: string }
type Aggregate = { constraints: Constraint[]; constraintsTotal: number; constraintsTruncated: boolean; readyOutputs: ReadyOutput[]; readyOutputsTotal: number; readyOutputsTruncated: boolean }

function monday(d: string) {
  const day = (new Date(`${d}T00:00:00Z`).getUTCDay() + 6) % 7
  return addDays(d, -day)
}

function dateValue(d: string) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(d)) return null
  const value = Date.parse(`${d}T00:00:00Z`)
  return Number.isFinite(value) && new Date(value).toISOString().slice(0, 10) === d ? value : null
}

/** Packet 032 read-only readiness window. Source-backed readiness aggregation is not yet exposed by the API. */
export function ReadinessTab() {
  const p = useCurrentProject()
  const lookahead = useMe().settings.coordinationLookaheadWeeks
  const [sp, setSp] = useSearchParams()
  const defaultFrom = monday(today())
  const from = sp.get('from') ?? defaultFrom
  const to = sp.get('to') ?? addDays(defaultFrom, lookahead * 7 - 1)
  const set = (key: string, value: string) => {
    const next = new URLSearchParams(sp)
    if (value) next.set(key, value); else next.delete(key)
    setSp(next, { replace: true })
  }
  const reset = () => setSp(new URLSearchParams(), { replace: true })
  const weeks = useMemo(() => {
    const fromValue = dateValue(from)
    const toValue = dateValue(to)
    if (fromValue === null || toValue === null || toValue < fromValue || toValue - fromValue > 83 * 86400000) return []
    const first = monday(from)
    const last = monday(to)
    const result: string[] = []
    for (let w = first; w <= last; w = addDays(w, 7)) result.push(w)
    return result
  }, [from, to])
  const invalidWindow = weeks.length === 0 || weeks.length > 12
  const q = useQuery({
    queryKey: ['p', p.id, 'readiness-window', from, to, weeks],
    enabled: !invalidWindow,
    queryFn: async () => {
      const [aggregate, ...pages] = await Promise.all([
        get<Aggregate>(`projects/${p.id}/readiness/window?from=${from}&to=${to}`),
        ...weeks.map((week) => get<Weekly>(`projects/${p.id}/weekly-commitments?weekStart=${week}`)),
      ])
      return {
        ...aggregate,
        commitments: pages.flatMap((page) => page.commitments),
        total: pages.reduce((n, page) => n + page.total, 0),
        truncated: pages.some((page) => page.truncated),
        truncatedWeeks: pages.filter((page) => page.truncated).map((page) => page.commitments[0]?.weekStart).filter((week): week is string => !!week),
        snapshots: pages.flatMap((page) => page.snapshots),
      }
    },
  })
  const filters = <div className="flex flex-wrap items-end gap-2 rounded-lg border bg-card p-3">
    <label className="text-xs text-muted-foreground">{t('readiness.from')}<Input type="date" className="mt-1 h-8 w-36" value={from} onChange={(e) => set('from', e.target.value)} /></label>
    <label className="text-xs text-muted-foreground">{t('readiness.to')}<Input type="date" className="mt-1 h-8 w-36" value={to} onChange={(e) => set('to', e.target.value)} /></label>
    <Button size="sm" variant="ghost" onClick={reset}>{t('common.clear')}</Button>
    <span className="ml-auto text-xs text-muted-foreground">{t('readiness.windowNote', { n: lookahead })}</span>
  </div>
  if (invalidWindow) return <Page title={t('readiness.title')} subtitle={t('readiness.subtitle')}>
    {filters}<div role="alert" className="rounded border border-bad/30 bg-bad-bg px-3 py-2 text-sm text-bad">{t('readiness.invalidWindow')}</div>
  </Page>
  if (q.isPending) return <Loading rows={8} />
  if (q.error) return <div className="p-6"><ErrorBanner error={q.error} retry={() => q.refetch()} /></div>
  const data = q.data
  const shown = data.commitments.filter((c) => c.targetDate >= from && c.targetDate <= to)
  const snapshotByWeek = new Map(data.snapshots.map((s) => [s.weekStart, s]))
  const base = `/projects/${p.projectNumber}`
  const workLink = (c: Commitment) => `${base}/${c.targetType === 'Task' ? 'tasks' : 'deliverables'}?panel=${c.targetType}:${c.targetId}`
  return (
    <Page title={t('readiness.title')} subtitle={t('readiness.subtitle')} actions={
      <Button asChild variant="outline" size="sm"><Link to={`${base}/coordination?meeting=1`}><CalendarCheck className="size-4" />{t('readiness.meeting')}</Link></Button>
    }>
      {filters}
      <div className="grid gap-4 md:grid-cols-2">
        <Section title={t('readiness.constraints')} id="constraints" count={data.constraintsTotal}>
          {data.constraintsTruncated && <p role="status" className="border-b border-warn/30 bg-warn-bg px-4 py-2 text-xs text-warn">{t('readiness.aggregateTruncated')}</p>}
          {data.constraints.length === 0 ? <p className="px-4 py-4 text-sm text-muted-foreground">{t('readiness.noConstraints')}</p> : <ul className="divide-y">{data.constraints.map((c) => <li key={c.id} className="px-4 py-3 text-sm">
            <Link className="font-medium text-primary hover:underline" to={`${base}/${c.targetType === 'Task' ? 'tasks' : 'deliverables'}?panel=${c.targetType}:${c.targetId}`}>{c.category} · {c.targetType}</Link>
            <p className="mt-1">{c.description}</p><p className="text-xs text-muted-foreground">{fmtDate(c.neededBy)} · <a className="underline" href={c.sourceUrl} target="_blank" rel="noreferrer">{t('readiness.source')}</a></p>
          </li>)}</ul>}
        </Section>
        <Section title={t('readiness.readyOutputs')} id="ready-outputs" count={data.readyOutputsTotal}>
          {data.readyOutputsTruncated && <p role="status" className="border-b border-warn/30 bg-warn-bg px-4 py-2 text-xs text-warn">{t('readiness.aggregateTruncated')}</p>}
          {data.readyOutputs.length === 0 ? <p className="px-4 py-4 text-sm text-muted-foreground">{t('readiness.noReadyOutputs')}</p> : <ul className="divide-y">{data.readyOutputs.map((o) => <li key={o.id} className="px-4 py-3 text-sm">
            <Link className="font-medium text-primary hover:underline" to={`${base}/${o.targetType === 'Task' ? 'tasks' : 'deliverables'}?panel=${o.targetType}:${o.targetId}`}>{o.key} · {o.name}</Link>
            <p className="mt-1">{o.intendedOutput}</p><p className="text-xs text-muted-foreground">{o.dueDate ? fmtDate(o.dueDate) : t('readiness.noDueDate')} · {o.completionCriteria}</p>
          </li>)}</ul>}
        </Section>
      </div>
      {data.truncated && <div role="status" className="rounded border border-warn/30 bg-warn-bg px-3 py-2 text-sm text-warn">{t('readiness.truncated', { n: data.total })}</div>}
      {shown.length === 0 && !data.truncated && <div className="rounded-lg border bg-card"><Empty>{t('readiness.empty')}</Empty></div>}
      {weeks.map((week) => {
        const rows = shown.filter((c) => c.weekStart === week)
        const snapshot = snapshotByWeek.get(week)
        return <Section key={week} title={t('readiness.week', { date: fmtDate(week) })} count={rows.length}>
          {snapshot && <div className="flex flex-wrap gap-x-4 gap-y-1 border-b bg-muted/30 px-4 py-2 text-xs text-muted-foreground">
            <span>{t('readiness.snapshot', { n: snapshot.committedCount })}</span>
            <span>{t('readiness.met', { n: snapshot.met, total: snapshot.committedCount })}</span>
            {snapshot.withdrawn > 0 && <span>{t('readiness.withdrawn', { n: snapshot.withdrawn })}</span>}
          </div>}
          {data.truncatedWeeks.includes(week) && <p role="status" className="border-b border-warn/30 bg-warn-bg px-4 py-2 text-xs text-warn">{t('readiness.weekTruncated')}</p>}
          {rows.length === 0 ? <p className="px-4 py-3 text-sm text-muted-foreground">{t(data.truncatedWeeks.includes(week) ? 'readiness.weekTruncated' : 'readiness.noWeekCommitments')}</p> : <ul className="divide-y">{rows.map((c) => <li key={c.id} className="flex flex-wrap items-start gap-3 px-4 py-3 text-sm">
            <div className="min-w-0 flex-1"><Link className="font-medium text-primary hover:underline" to={workLink(c)}>{c.targetType} <span className="font-mono text-xs">{c.targetId.slice(0, 8)}</span></Link><p className="mt-1">{c.intendedOutput}</p><p className="text-xs text-muted-foreground">{t('readiness.criteria')}: {c.completionCriteria}</p></div>
            <div className="flex shrink-0 flex-wrap items-center gap-2"><span className="text-xs text-muted-foreground">{fmtDate(c.targetDate)}</span><StatusPill status={c.state} />{c.readinessAtCommit ? <Chip tone="done">{tv(c.readinessAtCommit)}</Chip> : <Chip tone="idle">{t('readiness.notRecorded')}</Chip>}</div>
          </li>)}</ul>}
        </Section>
      })}
      <p className="text-xs text-muted-foreground"><ExternalLink className="mr-1 inline size-3" aria-hidden />{t('readiness.sourceNote')}</p>
    </Page>
  )
}
