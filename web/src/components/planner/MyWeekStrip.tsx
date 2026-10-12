import { useQuery } from '@tanstack/react-query'
import { ChevronDown } from 'lucide-react'
import { Link, useSearchParams } from 'react-router'
import { useEffect, useState, type ReactNode } from 'react'
import { ContributionList } from './ContributionList'
import { EntryPanel } from './EntryPanel'
import { Notice, Loading } from '@/components/hub/common'
import { Chip } from '@/components/hub/pills'
import { Button } from '@/components/ui/button'
import { api, get, qs } from '@/lib/api'
import { shortDate } from '@/lib/format'
import { t } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { PlannerGrid } from './PlannerGrid'
import { hoursLabel, type PlannerEntry, type PlannerGridData } from './labels'

/** My Week on My Work (§41 R10): one summary line for the first week shown, with its most severe indicator; the six-week
 *  strip opens on request, and a link to a week or a planning entry opens it straight away. */
export function MyWeekStrip({ personId, canPlan: _canPlan, id }: { personId: string; canPlan: boolean; id?: string }) {
  const [sp, setSp] = useSearchParams(); const requestedWeek = sp.get('myWeek'); const requestedEntry = sp.get('planningEntry')
  const [open, setOpen] = useState(!!(requestedWeek || requestedEntry))
  const [wide, setWide] = useState(() => window.matchMedia('(min-width: 1280px)').matches)
  useEffect(() => { const media = window.matchMedia('(min-width: 1280px)'); const update = () => setWide(media.matches); media.addEventListener('change', update); return () => media.removeEventListener('change', update) }, [])
  const entryQ = useQuery({ queryKey: ['my-week-entry', requestedEntry], queryFn: () => get<PlannerEntry>(`planning/entries/${requestedEntry}`), enabled: !!requestedEntry })
  const [entry, setEntry] = useState<PlannerEntry | null>(null); const [cell, setCell] = useState<string | null>(null)
  const [desktop, setDesktop] = useState(() => typeof window !== 'undefined' && window.matchMedia('(min-width: 768px)').matches)
  useEffect(() => { const m = window.matchMedia('(min-width: 768px)'); const update = () => setDesktop(m.matches); update(); m.addEventListener('change', update); return () => m.removeEventListener('change', update) }, [])
  const q = useQuery({ queryKey: ['my-week', personId, requestedWeek], queryFn: () => get<PlannerGridData>(`planning/grid${qs({ personId, from: requestedWeek, weeks: 6, includeMyDrafts: 'false' })}`) })
  const mine = q.data?.people.find((p) => p.id === personId)
  const first = mine?.cells.find((c) => c.week === q.data?.currentWeek) ?? mine?.cells[0]
  const signal = !first ? null : first.overPlanned ? <Chip tone="warn">{t('planner.indicator.over')}</Chip>
    : first.staleEntries > 0 ? <Chip tone="warn">{t('planner.indicator.stale')}</Chip> : first.underPlanned && <Chip tone="idle">{t('planner.indicator.under')}</Chip>
  const strip = (): ReactNode => {
    if (q.isPending) return <Loading rows={3} />
    if (q.error || !q.data) return <Notice title={t('planner.myWeekUnavailable')} action={_canPlan ? <Button asChild variant="outline"><Link to="/planner">{t('planner.open')}</Link></Button> : <Button variant="outline" onClick={() => void q.refetch()}>{t('app.retry')}</Button>}>{t('planner.myWeekUnavailableHint')}</Notice>
    if (!desktop) return <div className="space-y-3">{q.data.weeks.map((week) => <section key={week} className="rounded-lg border bg-card p-3"><h3 className="font-medium">{week}</h3>{q.data.people.flatMap((p) => p.entries.filter((e) => e.startWeek <= week && e.endWeek >= week && e.visibility !== 'Draft').map((e) => <p className="mt-2 text-sm" key={e.id}>{e.label} · {e.hoursPerWeek} h · {t(e.ownerKind === 'Self' ? 'planner.visibility.selfEntered' : 'planner.visibility.proposed')}</p>))}<Button size="sm" variant="outline" className="mt-2" onClick={() => setCell(`${personId}:${week}`)}>{t('planner.contributions')}</Button></section>)}{cell && <ContributionList personId={personId} week={cell.split(':')[1]} includeMyDrafts={false} onClose={() => setCell(null)} />}</div>
    const quickAdd = async (target: string, week: string, text: string, idempotencyKey: string) => { await api('planning/entries/quick', { method: 'POST', idempotencyKey, body: { personId: target, week, text } }); q.refetch() }
    return <section className="space-y-3"><div className="flex flex-wrap items-center justify-between gap-2"><p className="text-sm text-muted-foreground">{t('planner.myWeekHint')}</p>{_canPlan && <Button asChild variant="outline"><Link to={`/planner?personId=${encodeURIComponent(personId)}`}>{t('planner.open')}</Link></Button>}</div><PlannerGrid data={q.data} editable={(q.data.people.find((person) => person.id === personId)?.canCreateSelfEntry ?? false) && wide} onQuickAdd={quickAdd} onCell={(person, c) => setCell(`${person.id}:${c.week}`)} onEntry={setEntry} />{cell && <ContributionList personId={cell.split(':')[0]} week={cell.split(':')[1]} includeMyDrafts={false} onClose={() => setCell(null)} />}{(entry ?? entryQ.data) && <EntryPanel key={(entry ?? entryQ.data)!.id} entry={entry ?? entryQ.data ?? null} open readOnly={!wide || (entry ?? entryQ.data)?.ownerKind !== 'Self'} onClose={() => { setEntry(null); const next = new URLSearchParams(sp); next.delete('planningEntry'); setSp(next, { replace: true }) }} onChanged={() => void q.refetch()} />}</section>
  }
  return (
    <section id={id} aria-labelledby="my-week-h" className="overflow-hidden rounded-lg border bg-card">
      <div className="flex flex-wrap items-center gap-x-3 gap-y-1 px-5 py-2">
        <h2 id="my-week-h" className="text-base/6 font-semibold">{t('planner.myWeek')}</h2>
        <span className="text-sm text-muted-foreground tabular-nums">{q.isPending ? t('app.loading') : first
          ? t('mywork.weekSummary', { week: shortDate(first.week), planned: hoursLabel(first.confirmed + first.expected + first.possible), capacity: hoursLabel(first.capacity) })
          : t('planner.myWeekUnavailable')}</span>
        {signal}
        <Button variant="ghost" size="sm" className="ml-auto" aria-expanded={open} aria-controls="my-week-body" onClick={() => setOpen(!open)}>
          {open ? t('mywork.weekHide') : t('mywork.weekShow')}<ChevronDown className={cn('size-4', open && 'rotate-180')} aria-hidden />
        </Button>
      </div>
      <div id="my-week-body" hidden={!open} className="border-t p-4">{open && strip()}</div>
    </section>
  )
}
