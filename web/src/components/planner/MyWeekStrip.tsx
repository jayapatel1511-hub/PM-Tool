import { useQuery } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router'
import { useEffect, useState } from 'react'
import { ContributionList } from './ContributionList'
import { EntryPanel } from './EntryPanel'
import { Notice, Loading } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { api, get, qs } from '@/lib/api'
import { t } from '@/lib/i18n'
import { PlannerGrid } from './PlannerGrid'
import type { PlannerEntry, PlannerGridData } from './labels'

export function MyWeekStrip({ personId, canPlan: _canPlan }: { personId: string; canPlan: boolean }) {
  const [sp, setSp] = useSearchParams(); const requestedWeek = sp.get('myWeek'); const requestedEntry = sp.get('planningEntry')
  const [wide, setWide] = useState(() => window.matchMedia('(min-width: 1280px)').matches)
  useEffect(() => { const media = window.matchMedia('(min-width: 1280px)'); const update = () => setWide(media.matches); media.addEventListener('change', update); return () => media.removeEventListener('change', update) }, [])
  const entryQ = useQuery({ queryKey: ['my-week-entry', requestedEntry], queryFn: () => get<PlannerEntry>(`planning/entries/${requestedEntry}`), enabled: !!requestedEntry })
  const [entry, setEntry] = useState<PlannerEntry | null>(null); const [cell, setCell] = useState<string | null>(null)
  const [desktop, setDesktop] = useState(() => typeof window !== 'undefined' && window.matchMedia('(min-width: 768px)').matches)
  useEffect(() => { const m = window.matchMedia('(min-width: 768px)'); const update = () => setDesktop(m.matches); update(); m.addEventListener('change', update); return () => m.removeEventListener('change', update) }, [])
  const q = useQuery({ queryKey: ['my-week', personId, requestedWeek], queryFn: () => get<PlannerGridData>(`planning/grid${qs({ personId, from: requestedWeek, weeks: 6, includeMyDrafts: 'false' })}`) })
  if (q.isPending) return <Loading rows={3} />
  if (q.error || !q.data) return <Notice title={t('planner.myWeekUnavailable')} action={_canPlan ? <Button asChild variant="outline"><Link to="/planner">{t('planner.open')}</Link></Button> : <Button variant="outline" onClick={() => void q.refetch()}>{t('app.retry')}</Button>}>{t('planner.myWeekUnavailableHint')}</Notice>
  if (!desktop) return <Notice title={t('app.phoneNotice')} action={<Button asChild variant="outline"><Link to="/planner">{t('planner.open')}</Link></Button>}>{t('planner.phoneHint')}</Notice>
  const quickAdd = async (target: string, week: string, text: string, idempotencyKey: string) => { await api('planning/entries/quick', { method: 'POST', idempotencyKey, body: { personId: target, week, text } }); q.refetch() }
  return <section className="space-y-3"><div className="flex flex-wrap items-center justify-between gap-2"><p className="text-sm text-muted-foreground">{t('planner.myWeekHint')}</p>{_canPlan && <Button asChild variant="outline"><Link to={`/planner?personId=${encodeURIComponent(personId)}`}>{t('planner.open')}</Link></Button>}</div><PlannerGrid data={q.data} editable={(q.data.people.find((person) => person.id === personId)?.canCreateSelfEntry ?? false) && wide} onQuickAdd={quickAdd} onCell={(person, c) => setCell(`${person.id}:${c.week}`)} onEntry={setEntry} />{cell && <ContributionList personId={cell.split(':')[0]} week={cell.split(':')[1]} includeMyDrafts={false} onClose={() => setCell(null)} />}{(entry ?? entryQ.data) && <EntryPanel key={(entry ?? entryQ.data)!.id} entry={entry ?? entryQ.data ?? null} open readOnly={!wide || (entry ?? entryQ.data)?.ownerKind !== 'Self'} onClose={() => { setEntry(null); const next = new URLSearchParams(sp); next.delete('planningEntry'); setSp(next, { replace: true }) }} onChanged={() => void q.refetch()} />}</section>
}
