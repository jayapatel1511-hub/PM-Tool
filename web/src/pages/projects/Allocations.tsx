import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { ConfirmDialog, Empty, ErrorBanner, Field, Loading, Page } from '@/components/hub/common'
import { ExportMenu } from '@/components/hub/export'
import { ViewMenu } from '@/components/hub/views'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { get, post, qs } from '@/lib/api'
import { fmtDate, hours, today } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import { CommandForm, SelectField, type CoordOptions, WorkLink } from './CoordinationForms'
import { useCurrentProject } from './ProjectLayout'

type Allocation = { id: string; personId: string; personName: string; purpose: string; fromDate: string; throughDate: string; plannedHours: number; status: string; rowVersion: number; personState: 'Eligible' | 'Inactive' | 'Removed' | 'Missing' }
type AllocationPage = { items: Allocation[]; page: number; pageSize: number; totalCount: number }
type Detail = Allocation & { personEligible: boolean; confirmedBy?: string; confirmedAt?: string; links: { workType: string; workId: string; workDate: string; reviewHours?: number; reviewPackageId?: string }[]; days: { workDate: string; hours: number }[]; overCapacityWarningRecorded: boolean; canConfirm: boolean; canManage: boolean }
type Preview = { id: string; rowVersion: number; days: { date: string; availableHours: number; confirmedHours: number; proposedHours: number; resultingHours: number; overByHours: number; dateVersion: number }[] }
type ReviewOption = { id: string; reviewerId: string; dueDate: string; packageId: string; packageKey: string; packageTitle: string }

export function AllocationsTab() {
  const project = useCurrentProject(), qc = useQueryClient()
  const [sp, setSp] = useSearchParams(), [adding, setAdding] = useState(false)
  const selected = sp.get('allocation')
  const page = Math.max(1, Number(sp.get('page')) || 1)
  const filters = Object.fromEntries(['q', 'personId', 'purpose', 'status', 'from', 'to'].map(k => [k, sp.get(k) ?? '']))
  const open = (id: string | null) => { const next = new URLSearchParams(sp); if (id) next.set('allocation', id); else next.delete('allocation'); setSp(next) }
  const base = `projects/${project.id}/allocations`
  const list = useQuery({ queryKey: ['allocations', project.id, filters, page], queryFn: () => get<AllocationPage>(`${base}${qs({ ...filters, page })}`) })
  const options = useQuery({ queryKey: ['coord-options', project.id], queryFn: () => get<CoordOptions>(`projects/${project.id}/changes/options`) })
  const canPropose = project.permissions.isPm || (options.data?.manageDisciplineIds.length ?? 0) > 0
  const refresh = () => { qc.invalidateQueries({ queryKey: ['allocations', project.id] }); qc.invalidateQueries({ queryKey: ['workload'] }) }
  const change = (key: string, value: string) => { const next = new URLSearchParams(sp); value ? next.set(key, value) : next.delete(key); if (key !== 'page' && key !== 'allocation') next.delete('page'); setSp(next) }
  return <Page title={t('allocation.title')} subtitle={t('allocation.subtitle')}
    actions={<><ViewMenu listType="allocations" projectId={project.id} panelParam="allocation" /><ExportMenu path={`${base}/export`} params={filters} name={`${project.projectNumber}-allocations`} />{canPropose && <Button size="sm" onClick={() => setAdding(true)}>{t('allocation.new')}</Button>}</>}>
    <div className="flex flex-wrap items-end gap-3 rounded-lg border bg-card p-3">
      <Field label={t('common.search')} htmlFor="allocation-search"><Input id="allocation-search" type="search" value={filters.q} onChange={e => change('q', e.target.value)} /></Field>
      <SelectField label={t('workload.person')} value={filters.personId} onChange={v => change('personId', v)} required={false} choices={options.data?.people.map(p => ({ value: p.id, label: p.displayName })) ?? []} />
      <SelectField label={t('allocation.purpose')} value={filters.purpose} onChange={v => change('purpose', v)} required={false} choices={['Production', 'Review'].map(v => ({ value: v, label: t(`allocation.purpose.${v}`) }))} />
      <SelectField label={t('common.status')} value={filters.status} onChange={v => change('status', v)} required={false} choices={['Proposed', 'Confirmed', 'Declined', 'Cancelled', 'Completed'].map(v => ({ value: v, label: tv(v) }))} />
      <Field label={t('allocation.from')} htmlFor="allocation-filter-from"><Input id="allocation-filter-from" type="date" value={filters.from} onChange={e => change('from', e.target.value)} /></Field>
      <Field label={t('allocation.through')} htmlFor="allocation-filter-to"><Input id="allocation-filter-to" type="date" value={filters.to} onChange={e => change('to', e.target.value)} /></Field>
    </div>
    {options.error && <ErrorBanner error={options.error} retry={() => options.refetch()} />}
    {list.isPending ? <Loading rows={4} /> : list.error ? <ErrorBanner error={list.error} retry={() => list.refetch()} /> : !list.data.items.length ?
      <Empty>{t('allocation.empty')}</Empty> : <div className="overflow-x-auto rounded-lg border bg-card"><table className="w-full text-left text-sm">
        <caption className="sr-only">{t('allocation.title')}</caption><thead className="bg-muted/60"><tr>
          {[t('workload.person'), t('allocation.purpose'), t('allocation.dates'), t('allocation.hours'), t('common.status')].map(h => <th key={h} scope="col" className="p-3">{h}</th>)}
        </tr></thead><tbody>{list.data.items.map(a => <tr key={a.id} className="border-t">
          <td className="p-3"><button className="text-left font-medium text-primary underline" onClick={() => open(a.id)}>{a.personName}</button>{a.personState !== 'Eligible' && ['Proposed', 'Confirmed'].includes(a.status) && <p className="mt-1 text-warn">{t(a.personState === 'Inactive' ? 'allocation.personInactiveWarning' : a.personState === 'Removed' ? 'allocation.personRemovedWarning' : 'allocation.personMissingWarning')}</p>}</td>
          <td className="p-3">{t(`allocation.purpose.${a.purpose}`)}</td><td className="p-3 whitespace-nowrap">{fmtDate(a.fromDate)}–{fmtDate(a.throughDate)}</td>
          <td className="p-3 tabular-nums">{a.plannedHours}</td><td className="p-3">{tv(a.status)}</td>
        </tr>)}</tbody></table></div>}
    {!list.isPending && !list.error && <div className="flex items-center justify-end gap-3"><Button size="sm" variant="outline" disabled={page <= 1} onClick={() => change('page', String(page - 1))}>{t('handoff.previous')}</Button><span className="text-sm">{t('handoff.page', { n: page })}</span><Button size="sm" variant="outline" disabled={page * list.data.pageSize >= list.data.totalCount} onClick={() => change('page', String(page + 1))}>{t('handoff.next')}</Button></div>}
    {adding && options.data && <Proposal base={base} options={options.data} close={() => setAdding(false)} done={id => { setAdding(false); refresh(); open(id) }} />}
    {selected && <AllocationDetail base={base} id={selected} projectNumber={project.projectNumber} options={options.data}
      close={() => open(null)} refresh={refresh} />}
  </Page>
}

type LinkDraft = { key: string; workId: string; workDate: string; reviewHours: string }
type DayDraft = { key: string; workDate: string; hours: string }
function Proposal({ base, options, close, done, existing }: { base: string; options: CoordOptions; close: () => void; done: (id: string) => void; existing?: Detail }) {
  const [purpose, setPurpose] = useState<'Production' | 'Review'>((existing?.purpose as 'Production' | 'Review') ?? 'Production')
  const [personId, setPersonId] = useState(existing?.personId ?? ''), [fromDate, setFromDate] = useState(existing?.fromDate ?? today()), [throughDate, setThroughDate] = useState(existing?.throughDate ?? today())
  const [plannedHours, setPlannedHours] = useState(existing ? String(existing.plannedHours) : '')
  const [links, setLinks] = useState<LinkDraft[]>(() => existing?.links.map(l => ({ key: crypto.randomUUID(), workId: l.workId, workDate: l.workDate, reviewHours: l.reviewHours == null ? '' : String(l.reviewHours) })) ??
    [{ key: crypto.randomUUID(), workId: '', workDate: today(), reviewHours: '' }])
  const [days, setDays] = useState<DayDraft[]>(() => existing?.days.map(d => ({ key: crypto.randomUUID(), workDate: d.workDate, hours: String(d.hours) })) ?? [])
  const [reason, setReason] = useState('')
  const reviewOptions = useQuery({ queryKey: ['allocation-review-options', base], queryFn: () => get<ReviewOption[]>(`${base}/review-options`) })
  const tasks = options.tasks.filter(w => w.ownerId === personId && !['Complete', 'Cancelled', 'On Hold'].includes(w.status))
  const reviews = (reviewOptions.data ?? []).filter(r => r.reviewerId === personId)
  const choices = purpose === 'Production' ? tasks.map(w => ({ value: w.id, label: `${w.key} · ${w.name}` })) :
    reviews.map(r => ({ value: r.id, label: `${r.packageKey} · ${r.packageTitle} · ${fmtDate(r.dueDate)}` }))
  const updateLink = (key: string, change: Partial<LinkDraft>) => setLinks(rows => rows.map(row => row.key === key ? { ...row, ...change } : row))
  const updateDay = (key: string, change: Partial<DayDraft>) => setDays(rows => rows.map(row => row.key === key ? { ...row, ...change } : row))
  return <CommandForm path={existing ? `${base}/${existing.id}/edit` : base} title={t(existing ? 'allocation.edit' : 'allocation.new')}
    hint={t(existing?.status === 'Confirmed' ? 'allocation.editConfirmedHint' : 'allocation.proposalHint')} onClose={close} onDone={done}
    payload={() => ({ ...(existing ? { rowVersion: existing.rowVersion, reason } : {}), personId, purpose, fromDate, throughDate,
      plannedHours: Number(plannedHours), days: days.map(d => ({ workDate: d.workDate, hours: Number(d.hours) })),
      links: links.map(l => ({ workType: purpose === 'Production' ? 'Task' : 'Review', workId: l.workId, workDate: l.workDate,
        reviewHours: purpose === 'Review' ? Number(l.reviewHours) : null })) })}>
    <SelectField label={t('allocation.purpose')} value={purpose} onChange={v => { setPurpose(v as 'Production' | 'Review'); setLinks([{ key: crypto.randomUUID(), workId: '', workDate: fromDate, reviewHours: '' }]) }}
      choices={[{ value: 'Production', label: t('allocation.purpose.Production') }, { value: 'Review', label: t('allocation.purpose.Review') }]} />
    <SelectField label={t('workload.person')} value={personId} onChange={v => { setPersonId(v); setLinks([{ key: crypto.randomUUID(), workId: '', workDate: fromDate, reviewHours: '' }]) }} choices={options.people.map(p => ({ value: p.id, label: p.displayName }))} />
    {purpose === 'Review' && reviewOptions.error && <ErrorBanner error={reviewOptions.error} retry={() => reviewOptions.refetch()} />}
    {!choices.length && personId && <p className="text-sm text-muted-foreground">{t(purpose === 'Production' ? 'allocation.noTasks' : 'allocation.noReviews')}</p>}
    <div className="grid gap-3 sm:grid-cols-2"><Field label={t('allocation.from')} htmlFor="allocation-from"><Input id="allocation-from" type="date" required value={fromDate} onChange={e => setFromDate(e.target.value)} /></Field>
      <Field label={t('allocation.through')} htmlFor="allocation-through"><Input id="allocation-through" type="date" required min={fromDate} value={throughDate} onChange={e => setThroughDate(e.target.value)} /></Field></div>
    <Field label={t('allocation.plannedHours')} htmlFor="allocation-hours"><Input id="allocation-hours" type="number" required min="0.01" max="10000" step="0.01" value={plannedHours} onChange={e => setPlannedHours(e.target.value)} /></Field>
    <fieldset className="space-y-3 rounded border p-3"><legend className="px-1 font-medium">{t('allocation.linked')}</legend>{links.map((l, i) => <div className="space-y-3 rounded border p-3" key={l.key}>
      <SelectField label={`${t(purpose === 'Production' ? 'allocation.task' : 'allocation.reviewAssignment')} ${i + 1}`} value={l.workId} onChange={workId => updateLink(l.key, { workId })} choices={choices} />
      <div className="grid gap-3 sm:grid-cols-2"><Field label={`${t('allocation.linkDate')} ${i + 1}`} htmlFor={`allocation-work-${l.key}`}><Input id={`allocation-work-${l.key}`} type="date" required min={fromDate} max={throughDate} value={l.workDate} onChange={e => updateLink(l.key, { workDate: e.target.value })} /></Field>
        {purpose === 'Review' && <Field label={`${t('allocation.reviewEffort')} ${i + 1}`} htmlFor={`allocation-review-hours-${l.key}`}><Input id={`allocation-review-hours-${l.key}`} type="number" required min="0.01" max="10000" step="0.01" value={l.reviewHours} onChange={e => updateLink(l.key, { reviewHours: e.target.value })} /></Field>}</div>
      {links.length > 1 && <Button type="button" size="sm" variant="outline" onClick={() => setLinks(rows => rows.filter(row => row.key !== l.key))}>{t('allocation.removeLink')} {i + 1}</Button>}
    </div>)}<Button type="button" size="sm" variant="outline" disabled={links.length >= 500} onClick={() => setLinks(rows => [...rows, { key: crypto.randomUUID(), workId: '', workDate: fromDate, reviewHours: '' }])}>{t('allocation.addLink')}</Button></fieldset>
    <fieldset className="space-y-3 rounded border p-3"><legend className="px-1 font-medium">{t('allocation.daySplits')}</legend><p className="text-xs text-muted-foreground">{t('allocation.daySplitsHint')}</p>{days.map((d, i) => <div className="grid gap-3 sm:grid-cols-[1fr_1fr_auto]" key={d.key}>
      <Field label={`${t('allocation.date')} ${i + 1}`} htmlFor={`allocation-day-${d.key}`}><Input id={`allocation-day-${d.key}`} type="date" required min={fromDate} max={throughDate} value={d.workDate} onChange={e => updateDay(d.key, { workDate: e.target.value })} /></Field>
      <Field label={`${t('allocation.hours')} ${i + 1}`} htmlFor={`allocation-day-hours-${d.key}`}><Input id={`allocation-day-hours-${d.key}`} type="number" required min="0" max="10000" step="0.01" value={d.hours} onChange={e => updateDay(d.key, { hours: e.target.value })} /></Field>
      <Button type="button" size="sm" variant="outline" className="self-end" onClick={() => setDays(rows => rows.filter(row => row.key !== d.key))}>{t('allocation.removeDay')} {i + 1}</Button>
    </div>)}<Button type="button" size="sm" variant="outline" disabled={days.length >= 366} onClick={() => setDays(rows => [...rows, { key: crypto.randomUUID(), workDate: fromDate, hours: '' }])}>{t('allocation.addDay')}</Button></fieldset>
    {existing && <Field label={t('common.reason')} htmlFor="allocation-reason"><Textarea id="allocation-reason" required minLength={5} value={reason} onChange={e => setReason(e.target.value)} /></Field>}
  </CommandForm>
}

function AllocationDetail({ base, id, projectNumber, options, close, refresh }: { base: string; id: string; projectNumber: string; options?: CoordOptions; close: () => void; refresh: () => void }) {
  const [preview, setPreview] = useState<Preview | null>(null), [action, setAction] = useState<'confirm' | 'decline' | 'cancel' | 'complete' | null>(null)
  const [editing, setEditing] = useState(false)
  const [error, setError] = useState<unknown>(null)
  const detail = useQuery({ queryKey: ['allocation-detail', base, id], queryFn: () => get<Detail>(`${base}/${id}`) })
  const row = detail.data
  const done = () => { setAction(null); setPreview(null); setError(null); detail.refetch(); refresh() }
  const startConfirm = async () => { setError(null); try { setPreview(await get<Preview>(`${base}/${id}/confirmation-preview`)); setAction('confirm') } catch (e) { setError(e) } }
  if (editing && row && options) return <Proposal base={base} options={options} existing={row} close={() => setEditing(false)} done={() => { setEditing(false); done() }} />
  return <Dialog open onOpenChange={o => !o && close()}><DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
      <DialogHeader><DialogTitle>{t('allocation.detail')}</DialogTitle></DialogHeader>
      {detail.isPending ? <Loading rows={4} /> : detail.error ? <ErrorBanner error={detail.error} retry={() => detail.refetch()} /> : row && <div className="space-y-4 text-sm">
        <p>{row.personName} · {t(`allocation.purpose.${row.purpose}`)} · {row.status}</p>
        {!row.personEligible && <div role="alert" className="rounded border border-warn p-3 text-warn">
          <p>{t(row.personState === 'Inactive' ? 'allocation.personInactiveWarning' : row.personState === 'Removed' ? 'allocation.personRemovedWarning' : 'allocation.personMissingWarning')}</p>
          {row.canManage && <p className="mt-1">{t('allocation.replacePersonHint')}</p>}
        </div>}
        <p>{fmtDate(row.fromDate)}–{fmtDate(row.throughDate)} · {row.plannedHours} {t('allocation.hours')}</p>
        {row.confirmedAt && <p className="text-muted-foreground">{t('allocation.confirmationProvenance')} · {fmtDate(row.confirmedAt)}</p>}
        {row.overCapacityWarningRecorded && <p className="rounded border border-warn p-2 text-warn">{t('allocation.overCapacityRecorded')}</p>}
        {row.days.length > 0 && <section><h3 className="font-medium">{t('allocation.daySplits')}</h3><ul className="mt-1 space-y-1">{row.days.map(d =>
          <li key={d.workDate}>{fmtDate(d.workDate)} · {d.hours} {t('allocation.hours')}</li>)}</ul></section>}
        <h3 className="font-medium">{t('allocation.linked')}</h3><ul className="space-y-2">{row.links.map(l => <li key={`${l.workType}:${l.workId}:${l.workDate}`}>
          {l.workType === 'Review' ? <Link className="text-primary underline" to={`/projects/${projectNumber}/reviews${l.reviewPackageId ? `?panel=ReviewPackage:${l.reviewPackageId}` : ''}`}>{t('allocation.reviewAssignment')}</Link> :
            options ? <WorkLink options={options} type={l.workType} id={l.workId} number={projectNumber} /> :
              <Link className="text-primary underline" to={`/projects/${projectNumber}/tasks?panel=Task:${l.workId}`}>{t('allocation.openTask')}</Link>} · {fmtDate(l.workDate)}{l.reviewHours != null && ` · ${l.reviewHours} h`}
        </li>)}</ul>
        <div className="flex flex-wrap gap-2">{row.status === 'Proposed' && row.canConfirm && <><Button size="sm" onClick={startConfirm}>{t('allocation.reviewCapacity')}</Button>
          <Button size="sm" variant="outline" onClick={() => setAction('decline')}>{t('allocation.decline')}</Button></>}
          {row.canManage && ['Proposed', 'Confirmed'].includes(row.status) && <Button size="sm" variant="outline" onClick={() => setAction('cancel')}>{t('allocation.cancel')}</Button>}
          {row.canManage && options && ['Proposed', 'Confirmed'].includes(row.status) && <Button size="sm" variant="outline" onClick={() => setEditing(true)}>{t('allocation.edit')}</Button>}
          {row.canManage && row.status === 'Confirmed' && <Button size="sm" variant="outline" onClick={() => setAction('complete')}>{t('allocation.complete')}</Button>}
        </div>{error != null && <ErrorBanner error={error} />}
      </div>}

    {action && row && <ConfirmDialog open onOpenChange={o => !o && setAction(null)} title={t(`allocation.${action}`)}
      body={action === 'confirm' ? t('allocation.confirmHint') : t('allocation.reasonHint')}
      reason={action === 'confirm' ? (preview?.days.some(d => d.overByHours > 0) ? true : false) : true}
      onConfirm={async reason => {
        if (action === 'confirm' && !preview) return
        const payload = action === 'confirm' ? { requestId: crypto.randomUUID(), rowVersion: preview!.rowVersion,
          dateVersions: preview!.days.map(d => ({ workDate: d.date, rowVersion: d.dateVersion })), overCapacityReason: reason || null } :
          { requestId: crypto.randomUUID(), rowVersion: row.rowVersion, reason }
        await post(`${base}/${id}/${action}`, payload); toast.success(t('common.saved')); done()
      }}>
      {/* eslint-disable-next-line jsx-a11y/no-noninteractive-tabindex -- axe scrollable-region-focusable: keyboard users scroll the dated rows */}
      {action === 'confirm' && preview && <div className="max-h-56 overflow-auto rounded border text-sm" tabIndex={0} role="region" aria-label={t('allocation.reviewCapacity')}><table className="w-full text-left"><thead><tr>
        {[t('allocation.date'), t('allocation.available'), t('allocation.existing'), t('allocation.resulting'), t('allocation.over')].map(h => <th scope="col" key={h} className="p-2">{h}</th>)}
      </tr></thead><tbody>{preview.days.map(d => <tr key={d.date} className="border-t"><td className="p-2">{fmtDate(d.date)}</td><td className="p-2">{hours(d.availableHours)}</td>
        <td className="p-2">{hours(d.confirmedHours)}</td><td className="p-2">{hours(d.resultingHours)}</td><td className="p-2">{d.overByHours > 0 ? <strong className="text-bad">{hours(d.overByHours)}</strong> : hours(0)}</td></tr>)}</tbody></table></div>}
    </ConfirmDialog>}
  </DialogContent></Dialog>
}
