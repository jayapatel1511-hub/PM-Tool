import { useState, type ReactNode } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { useSearchParams } from 'react-router'
import { ActiveFilters, ChipToggle, Empty, ErrorBanner, Field, FilterBar, Loading, Missing, Page, TableRegion, tdCls, thCls } from '@/components/hub/common'
import { ExportMenu } from '@/components/hub/export'
import { ViewMenu } from '@/components/hub/views'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { get, qs } from '@/lib/api'
import { t, tv } from '@/lib/i18n'
import { fmtDate } from '@/lib/format'
import { cn } from '@/lib/utils'
import { useCurrentProject } from './ProjectLayout'
import { CoordStatus, Count, PersonLabel, RegisterPager, SelectField, discName, personName, peopleChoices, disciplineChoices, useCoordRefresh, type CoordOptions, type RecordVersion } from './CoordinationForms'

type Row = RecordVersion & { key: string; title: string; status: string; coordinatorId?: string; ownerId?: string; outstandingDisciplines?: number; blockingFindings?: number; roundNumber?: number; waitingDays?: number; assessmentDueDate?: string; pendingAssessments?: number }
export type RegisterContext = { options: CoordOptions; projectId: string; number: string; refresh: () => void; open: (id: string) => void }
export function CoordinationRegister({ kind, statuses, create, detail, extra }: { kind: 'reviews' | 'changes'; statuses: string[]; create: (c: RegisterContext & { close: () => void }) => ReactNode; detail: (c: RegisterContext & { id: string; close: () => void }) => ReactNode; extra?: (c: RegisterContext) => ReactNode }) {
  const p = useCurrentProject(), [sp, setSp] = useSearchParams(), [adding, setAdding] = useState(false), refresh = useCoordRefresh(p.id)
  const type = kind === 'reviews' ? 'ReviewPackage' : 'ChangeNotice', page = Math.max(1, Number(sp.get('page')) || 1)
  const filters = Object.fromEntries(['q', 'status', 'ownerId', 'mine', ...(kind === 'reviews' ? ['disciplineId'] : [])].map(k => [k, sp.get(k) ?? '']))
  const options = useQuery({ queryKey: ['coord-options', p.id], queryFn: () => get<CoordOptions>(`projects/${p.id}/changes/options`) })
  const list = useQuery({ queryKey: [kind, p.id, filters, page], queryFn: () => get<{ items: Row[]; totalCount: number; pageSize: number }>(`projects/${p.id}/${kind}${qs({ ...filters, page })}`) })
  const change = (key: string, value: string) => { const next = new URLSearchParams(sp); if (value) next.set(key, value); else next.delete(key); if (key !== 'page' && key !== 'panel') next.delete('page'); setSp(next) }
  const open = (id: string) => change('panel', `${type}:${id}`), panel = sp.get('panel')?.startsWith(`${type}:`) ? sp.get('panel')!.slice(type.length + 1) : null
  const ctx = options.data ? { options: options.data, projectId: p.id, number: p.projectNumber, refresh, open } : null
  const reviews = kind === 'reviews', ownerLabel = t(reviews ? 'coord.coordinator' : 'coord.owner')
  const headings: [string, boolean?][] = reviews ? [['coord.item'], ['coord.coordinator'], ['common.status'], ['review.round', true], ['review.outstanding', true], ['review.blocking', true], ['review.waiting', true]] : [['coord.item'], ['coord.owner'], ['common.status'], ['change.due'], ['change.pending', true]]
  // Active filters as removable tokens (§13.0 Filters); the URL keeps the same parameters.
  const tokens = [
    filters.q && { key: 'q', label: t('common.search'), value: filters.q },
    filters.status && { key: 'status', label: t('common.status'), value: tv(filters.status) },
    filters.ownerId && { key: 'ownerId', label: ownerLabel, value: ctx ? personName(ctx.options, filters.ownerId) : t('common.dash') },
    filters.disciplineId && { key: 'disciplineId', label: t('coord.discipline'), value: ctx ? discName(ctx.options, filters.disciplineId) : t('common.dash') },
    filters.mine && { key: 'mine', label: t('coord.mine'), value: t('common.yes') },
  ].filter(x => !!x)
  const clear = () => { const next = new URLSearchParams(sp); for (const k of Object.keys(filters)) next.delete(k); next.delete('page'); setSp(next) }
  const num = cn(tdCls, 'text-right tabular-nums')
  return <Page title={t(`${kind}.title`)} subtitle={t(`${kind}.subtitle`)} actions={<><ViewMenu listType={kind} projectId={p.id} /><ExportMenu path={`projects/${p.id}/${kind}/export`} params={filters} name={`${p.projectNumber}-${kind}`} />{ctx?.options.canWrite && (kind === 'changes' || ctx.options.manageDisciplineIds.length > 0) && <Button onClick={() => setAdding(true)}><Plus className="size-4" />{t(`${kind}.new`)}</Button>}</>}>
    <FilterBar>
      <div className="flex flex-wrap items-end gap-3">
        <Field label={t('common.search')} htmlFor={`${kind}-search`} className="w-full sm:w-56"><Input id={`${kind}-search`} type="search" value={filters.q} onChange={e => change('q', e.target.value)} /></Field>
        <div className="w-full sm:w-44"><SelectField label={t('common.status')} value={filters.status} onChange={v => change('status', v)} required={false} choices={statuses.map(s => ({ value: s, label: tv(s) }))} /></div>
        {ctx && <div className="w-full sm:w-52"><SelectField label={ownerLabel} value={filters.ownerId} onChange={v => change('ownerId', v)} required={false} choices={peopleChoices(ctx.options)} /></div>}
        {ctx && reviews && <div className="w-full sm:w-48"><SelectField label={t('coord.discipline')} value={filters.disciplineId} onChange={v => change('disciplineId', v)} required={false} choices={disciplineChoices(ctx.options)} /></div>}
      </div>
      <div className="flex flex-wrap items-center gap-2"><ChipToggle on={filters.mine === 'true'} onClick={() => change('mine', filters.mine === 'true' ? '' : 'true')}>{t('coord.mine')}</ChipToggle></div>
      <ActiveFilters tokens={tokens} onRemove={k => change(k, '')} onClear={clear} />
    </FilterBar>
    {options.error && <ErrorBanner error={options.error} retry={() => options.refetch()} />}
    {list.isPending ? <div className="rounded-lg border bg-card"><Loading rows={4} /></div> : list.error ? <ErrorBanner error={list.error} retry={() => list.refetch()} /> : <>
      <p role="status" className="text-sm text-muted-foreground">{t('coord.count', { n: list.data.totalCount })}</p>
      {!list.data.items.length ? <div className="rounded-lg border bg-card"><Empty title={t(`${kind}.empty`)}>{t(tokens.length ? 'register.noMatch' : `${kind}.emptyHint`)}</Empty></div> :
        <TableRegion><table className="w-full text-left text-sm"><caption className="sr-only">{t(`${kind}.title`)}</caption>
          <thead className="bg-muted"><tr>{headings.map(([h, numeric]) => <th scope="col" key={h} className={cn(thCls, numeric && 'text-right')}>{t(h)}</th>)}</tr></thead>
          <tbody>{list.data.items.map(r => <tr key={r.id} className={cn('border-t hover:bg-muted', panel === r.id && 'bg-accent shadow-[inset_3px_0_0_var(--primary)] hover:bg-accent')}>
            <td className={cn(tdCls, 'min-w-64')}><button className="break-words text-left font-semibold text-primary underline-offset-4 hover:underline" aria-current={panel === r.id || undefined} onClick={() => open(r.id)}><span className="key font-normal">{r.key}</span> · {r.title}</button></td>
            <td className={tdCls}>{ctx ? <PersonLabel options={ctx.options} id={r.coordinatorId ?? r.ownerId} /> : <Missing />}</td>
            <td className={tdCls}><CoordStatus status={r.status} /></td>
            {reviews ? <><td className={num}>{r.roundNumber ?? <Missing />}</td><td className={num}>{r.outstandingDisciplines ?? <Missing />}</td><td className={num}><Count n={r.blockingFindings} tone="bad" /></td><td className={num}>{r.waitingDays ?? <Missing />}</td></>
              : <><td className={cn(tdCls, 'whitespace-nowrap tabular-nums')}>{r.assessmentDueDate ? fmtDate(r.assessmentDueDate) : <Missing />}</td><td className={num}><Count n={r.pendingAssessments} tone="warn" /></td></>}
          </tr>)}</tbody></table></TableRegion>}
      <RegisterPager label={t(`${kind}.pager`)} page={page} total={list.data.totalCount} pageSize={list.data.pageSize} onPage={n => change('page', String(n))} />
    </>}
    {ctx && extra?.(ctx)}
    {ctx && adding && create({ ...ctx, close: () => setAdding(false) })}
    {ctx && !adding && panel && detail({ ...ctx, id: panel, close: () => change('panel', '') })}
  </Page>
}
