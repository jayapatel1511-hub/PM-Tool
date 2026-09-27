import { useState, type ReactNode } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useSearchParams } from 'react-router'
import { ErrorBanner, Loading, Page, Field } from '@/components/hub/common'
import { ExportMenu } from '@/components/hub/export'
import { ViewMenu } from '@/components/hub/views'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { get, qs } from '@/lib/api'
import { t, tv } from '@/lib/i18n'
import { fmtDate } from '@/lib/format'
import { useCurrentProject } from './ProjectLayout'
import { SelectField, personName, peopleChoices, disciplineChoices, useCoordRefresh, type CoordOptions, type RecordVersion } from './CoordinationForms'

type Row = RecordVersion & { key: string; title: string; status: string; coordinatorId?: string; ownerId?: string; outstandingDisciplines?: number; blockingFindings?: number; roundNumber?: number; waitingDays?: number; assessmentDueDate?: string; pendingAssessments?: number }
export type RegisterContext = { options: CoordOptions; projectId: string; number: string; refresh: () => void; open: (id: string) => void }
export function CoordinationRegister({ kind, statuses, create, detail, extra }: { kind: 'reviews' | 'changes'; statuses: string[]; create: (c: RegisterContext & { close: () => void }) => ReactNode; detail: (c: RegisterContext & { id: string; close: () => void }) => ReactNode; extra?: (c: RegisterContext) => ReactNode }) {
  const p = useCurrentProject(), [sp, setSp] = useSearchParams(), [adding, setAdding] = useState(false), refresh = useCoordRefresh(p.id)
  const type = kind === 'reviews' ? 'ReviewPackage' : 'ChangeNotice', page = Math.max(1, Number(sp.get('page')) || 1)
  const filters = Object.fromEntries(['q', 'status', 'ownerId', 'mine', ...(kind === 'reviews' ? ['disciplineId'] : [])].map(k => [k, sp.get(k) ?? '']))
  const options = useQuery({ queryKey: ['coord-options', p.id], queryFn: () => get<CoordOptions>(`projects/${p.id}/changes/options`) })
  const list = useQuery({ queryKey: [kind, p.id, filters, page], queryFn: () => get<{ items: Row[]; totalCount: number; pageSize: number }>(`projects/${p.id}/${kind}${qs({ ...filters, page })}`) })
  const change = (key: string, value: string) => { const next = new URLSearchParams(sp); value ? next.set(key, value) : next.delete(key); if (key !== 'page' && key !== 'panel') next.delete('page'); setSp(next) }
  const open = (id: string) => change('panel', `${type}:${id}`), panel = sp.get('panel')?.startsWith(`${type}:`) ? sp.get('panel')!.slice(type.length + 1) : null
  const ctx = options.data ? { options: options.data, projectId: p.id, number: p.projectNumber, refresh, open } : null
  const headings = kind === 'reviews' ? ['coord.item', 'coord.coordinator', 'common.status', 'review.round', 'review.outstanding', 'review.blocking', 'review.waiting'] : ['coord.item', 'coord.owner', 'common.status', 'change.due', 'change.pending']
  return <Page title={t(`${kind}.title`)} subtitle={t(`${kind}.subtitle`)} actions={<><ViewMenu listType={kind} projectId={p.id} /><ExportMenu path={`projects/${p.id}/${kind}/export`} params={filters} name={`${p.projectNumber}-${kind}`} />{ctx?.options.canWrite && (kind === 'changes' || ctx.options.manageDisciplineIds.length > 0) && <Button size="sm" onClick={() => setAdding(true)}>{t(`${kind}.new`)}</Button>}</>}>
    <div className="flex flex-wrap items-end gap-3 rounded-lg border bg-card p-3"><Field label={t('common.search')} htmlFor={`${kind}-search`}><Input id={`${kind}-search`} type="search" value={filters.q} onChange={e => change('q', e.target.value)} /></Field>
      <SelectField label={t('common.status')} value={filters.status} onChange={v => change('status', v)} required={false} choices={statuses.map(s => ({ value: s, label: tv(s) }))} />
      {ctx && <SelectField label={t(kind === 'reviews' ? 'coord.coordinator' : 'coord.owner')} value={filters.ownerId} onChange={v => change('ownerId', v)} required={false} choices={peopleChoices(ctx.options)} />}
      {ctx && kind === 'reviews' && <SelectField label={t('coord.discipline')} value={filters.disciplineId} onChange={v => change('disciplineId', v)} required={false} choices={disciplineChoices(ctx.options)} />}
      <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={filters.mine === 'true'} onChange={e => change('mine', e.target.checked ? 'true' : '')} />{t('coord.mine')}</label>
    </div>
    {options.error && <ErrorBanner error={options.error} retry={() => options.refetch()} />}
    {list.isPending ? <Loading rows={4} /> : list.error ? <ErrorBanner error={list.error} retry={() => list.refetch()} /> : <>
      <p role="status" className="text-sm text-muted-foreground">{t('coord.count', { n: list.data.totalCount })}</p>
      {!list.data.items.length ? <div className="rounded-lg border bg-card p-8 text-center"><h2 className="font-medium">{t(`${kind}.empty`)}</h2><p className="mt-2 text-sm text-muted-foreground">{t(`${kind}.emptyHint`)}</p></div> : <div className="overflow-x-auto rounded-lg border bg-card"><table className="w-full text-left text-sm"><caption className="sr-only">{t(`${kind}.title`)}</caption><thead className="bg-muted/60"><tr>{headings.map(h => <th scope="col" key={h} className="p-3 font-medium">{t(h)}</th>)}</tr></thead><tbody>{list.data.items.map(r => <tr key={r.id} className="border-t align-top"><td className="min-w-56 p-3"><button className="text-left font-medium text-primary hover:underline" onClick={() => open(r.id)}>{r.key} · {r.title}</button></td><td className="p-3">{ctx && personName(ctx.options, r.coordinatorId ?? r.ownerId)}</td><td className="p-3">{tv(r.status)}</td>{kind === 'reviews' ? <><td className="p-3">{r.roundNumber}</td><td className="p-3">{r.outstandingDisciplines}</td><td className="p-3">{r.blockingFindings}</td><td className="p-3">{r.waitingDays}</td></> : <><td className="p-3 whitespace-nowrap">{fmtDate(r.assessmentDueDate)}</td><td className="p-3">{r.pendingAssessments}</td></>}</tr>)}</tbody></table></div>}
      <div className="flex justify-end items-center gap-3"><Button size="sm" variant="outline" disabled={page <= 1} onClick={() => change('page', String(page - 1))}>{t('handoff.previous')}</Button><span className="text-sm">{t('handoff.page', { n: page })}</span><Button size="sm" variant="outline" disabled={page * list.data.pageSize >= list.data.totalCount} onClick={() => change('page', String(page + 1))}>{t('handoff.next')}</Button></div>
    </>}
    {ctx && extra?.(ctx)}
    {ctx && adding && create({ ...ctx, close: () => setAdding(false) })}
    {ctx && !adding && panel && detail({ ...ctx, id: panel, close: () => change('panel', '') })}
  </Page>
}
