import { UrlSearchInput } from '@/components/hub/url-search'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { AlertTriangle, CheckCircle2, Lock, Monitor, Plus, X } from 'lucide-react'
import { useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { ConfirmDialog, DesktopOnly, Empty, ErrorBanner, Field, Loading, Notice, Page, TableRegion, tdCls as td, thCls as th } from '@/components/hub/common'
import { ExportMenu } from '@/components/hub/export'
import { Avatar } from '@/components/hub/people'
import { Pill, type Tone } from '@/components/hub/pills'
import { ViewMenu } from '@/components/hub/views'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Sheet, SheetContent, SheetDescription, SheetTitle } from '@/components/ui/sheet'
import { Textarea } from '@/components/ui/textarea'
import { type ApiError, get, post, qs } from '@/lib/api'
import { fmtDate, hours, today } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { CommandForm, SelectField, type CoordOptions, WorkLink } from './CoordinationForms'
import { useCurrentProject } from './ProjectLayout'

type Allocation = { id: string; personId: string; personName: string | null; purpose: string; fromDate: string; throughDate: string; plannedHours: number; status: string; rowVersion: number; personState: 'Eligible' | 'Inactive' | 'Removed' | 'Missing' }
type AllocationPage = { items: Allocation[]; page: number; pageSize: number; totalCount: number }
type Detail = Allocation & { personEligible: boolean; confirmedBy?: string; confirmedAt?: string; links: { workType: string; workId: string; workDate: string; reviewHours?: number; reviewPackageId?: string }[]; days: { workDate: string; hours: number }[]; overCapacityWarningRecorded: boolean; canConfirm: boolean; canManage: boolean }
type Preview = { id: string; rowVersion: number; days: { date: string; availableHours: number; confirmedHours: number; proposedHours: number; resultingHours: number; overByHours: number; dateVersion: number }[] }
type ReviewOption = { id: string; reviewerId: string; dueDate: string; packageId: string; packageKey: string; packageTitle: string }

/** Approval status (FR-CAP-01) with the §13.0 symbol: a request awaiting the supervisor, an accepted commitment, or closed.
 *  It is an approval state only; the column says so, so "Confirmed" is never read as a confidence or visibility level. */
const STATUS_TONE: Record<string, Tone> = { Proposed: 'work', Confirmed: 'done', Completed: 'done', Declined: 'idle', Cancelled: 'idle' }
const AllocationStatus = ({ status }: { status: string }) => <Pill tone={STATUS_TONE[status] ?? 'idle'}>{tv(status)}</Pill>

/** Purpose is a category (identity tint), not a status. */
const PURPOSE_ACCENT: Record<string, string> = { Production: 'blue', Review: 'lavender' }
const Purpose = ({ purpose }: { purpose: string }) => (
  <span data-accent={PURPOSE_ACCENT[purpose] ?? 'blue'} className="inline-flex items-center whitespace-nowrap rounded-md bg-(--acc-bg) px-2 py-0.5 text-xs/[18px] font-medium text-(--acc-fg)">{t(`allocation.purpose.${purpose}`)}</span>
)
const eligibility = (state: Allocation['personState']) => t(state === 'Inactive' ? 'allocation.personInactiveWarning' : state === 'Removed' ? 'allocation.personRemovedWarning' : 'allocation.personMissingWarning')

export function AllocationsTab() {
  const header = { title: t('allocation.title'), subtitle: t('allocation.subtitle') }
  return <DesktopOnly notice={<Page {...header}><Notice icon={Monitor} title={t('app.phoneNotice')}>{t('allocation.phoneHint')}</Notice></Page>}><Allocations {...header} /></DesktopOnly>
}

function Allocations(header: { title: string; subtitle: string }) {
  const project = useCurrentProject(), qc = useQueryClient()
  const [sp, setSp] = useSearchParams(), [adding, setAdding] = useState(false)
  const selected = sp.get('allocation')
  const page = Math.max(1, Number(sp.get('page')) || 1)
  const filters = Object.fromEntries(['q', 'personId', 'purpose', 'status', 'from', 'to'].map(k => [k, sp.get(k) ?? '']))
  const filtered = Object.values(filters).some(Boolean)
  const open = (id: string | null) => { const next = new URLSearchParams(sp); if (id) next.set('allocation', id); else next.delete('allocation'); setSp(next) }
  const base = `projects/${project.id}/allocations`
  const list = useQuery({ queryKey: ['allocations', project.id, filters, page], queryFn: () => get<AllocationPage>(`${base}${qs({ ...filters, page })}`) })
  const options = useQuery({ queryKey: ['coord-options', project.id], queryFn: () => get<CoordOptions>(`projects/${project.id}/changes/options`) })
  const canPropose = project.permissions.isPm || (options.data?.manageDisciplineIds.length ?? 0) > 0
  const refresh = () => { qc.invalidateQueries({ queryKey: ['allocations', project.id] }); qc.invalidateQueries({ queryKey: ['workload'] }) }
  const change = (key: string, value: string) => { const next = new URLSearchParams(sp); value ? next.set(key, value) : next.delete(key); if (key !== 'page' && key !== 'allocation') next.delete('page'); setSp(next) }
  const clear = () => { const next = new URLSearchParams(sp); for (const k of Object.keys(filters)) next.delete(k); next.delete('page'); setSp(next) }
  const pages = list.data ? Math.max(1, Math.ceil(list.data.totalCount / list.data.pageSize)) : 1
  const propose = <Button onClick={() => setAdding(true)}><Plus className="size-4" />{t('allocation.new')}</Button>
  return <Page {...header} actions={<><ViewMenu listType="allocations" projectId={project.id} panelParam="allocation" /><ExportMenu path={`${base}/export`} params={filters} name={`${project.projectNumber}-allocations`} />{canPropose && propose}</>}>
    <div className="flex flex-wrap items-end gap-3 rounded-lg border bg-card p-4 md:p-5">
      <Field label={t('common.search')} htmlFor="allocation-search" className="w-full sm:w-56"><UrlSearchInput id="allocation-search" value={filters.q} onValueChange={value => change('q', value)} /></Field>
      <div className="w-full sm:w-48"><SelectField label={t('workload.person')} value={filters.personId} onChange={v => change('personId', v)} required={false} choices={options.data?.people.map(p => ({ value: p.id, label: p.displayName })) ?? []} /></div>
      <div className="w-full sm:w-40"><SelectField label={t('allocation.purpose')} value={filters.purpose} onChange={v => change('purpose', v)} required={false} choices={['Production', 'Review'].map(v => ({ value: v, label: t(`allocation.purpose.${v}`) }))} /></div>
      <div className="w-full sm:w-44"><SelectField label={t('allocation.status')} value={filters.status} onChange={v => change('status', v)} required={false} choices={['Proposed', 'Confirmed', 'Declined', 'Cancelled', 'Completed'].map(v => ({ value: v, label: tv(v) }))} /></div>
      <Field label={t('allocation.from')} htmlFor="allocation-filter-from" className="w-full sm:w-40"><Input id="allocation-filter-from" type="date" value={filters.from} onChange={e => change('from', e.target.value)} /></Field>
      <Field label={t('allocation.through')} htmlFor="allocation-filter-to" className="w-full sm:w-40"><Input id="allocation-filter-to" type="date" value={filters.to} onChange={e => change('to', e.target.value)} /></Field>
      {filtered && <Button variant="link" className="px-1" onClick={clear}><X className="size-4" />{t('filters.clear')}</Button>}
    </div>
    {options.error && <ErrorBanner error={options.error} retry={() => options.refetch()} />}
    {list.isPending ? <div className="rounded-lg border bg-card"><Loading rows={4} /></div> : list.error ? <ErrorBanner error={list.error} retry={() => list.refetch()} /> : !list.data.items.length ?
      <div className="rounded-lg border bg-card">{filtered
        ? <Empty title={t('allocation.emptyFilteredTitle')} action={<Button variant="outline" onClick={clear}>{t('filters.clear')}</Button>}>{t('allocation.emptyFiltered')}</Empty>
        : <Empty title={t('allocation.empty')} action={canPropose && <Button variant="outline" onClick={() => setAdding(true)}><Plus className="size-4" />{t('allocation.new')}</Button>}>{t(canPropose ? 'allocation.emptyHint' : 'allocation.emptyReadOnly')}</Empty>}</div>
      : <TableRegion><table className="w-full text-left text-sm">
        <caption className="sr-only">{t('allocation.title')}</caption>
        <thead className="bg-muted"><tr>
          <th scope="col" className={th}>{t('workload.person')}</th><th scope="col" className={th}>{t('allocation.purpose')}</th><th scope="col" className={th}>{t('allocation.dates')}</th>
          <th scope="col" className={cn(th, 'text-right')}>{t('allocation.plannedHours')}</th><th scope="col" className={th}>{t('allocation.status')}</th>
        </tr></thead>
        <tbody>{list.data.items.map(a => <tr key={a.id} className={cn('border-t hover:bg-muted', selected === a.id && 'bg-accent shadow-[inset_3px_0_0_var(--primary)] hover:bg-accent')}>
          <td className={td}><div className="flex items-start gap-2.5">
            <Avatar id={a.personId} name={a.personName} />
            <div className="min-w-0">
              <button className="break-words text-left font-semibold text-primary underline-offset-4 hover:underline" aria-current={selected === a.id || undefined} onClick={() => open(a.id)}>{a.personName ?? t('allocation.personUnavailable')}</button>
              {a.personState !== 'Eligible' && ['Proposed', 'Confirmed'].includes(a.status) && <p className="mt-1 text-xs/[18px] text-warn"><span aria-hidden>▲ </span>{eligibility(a.personState)}</p>}
            </div>
          </div></td>
          <td className={td}><Purpose purpose={a.purpose} /></td>
          <td className={cn(td, 'whitespace-nowrap tabular-nums')}>{fmtDate(a.fromDate)} – {fmtDate(a.throughDate)}</td>
          <td className={cn(td, 'text-right font-medium tabular-nums')}>{hours(a.plannedHours)}</td>
          <td className={td}><AllocationStatus status={a.status} /></td>
        </tr>)}</tbody></table></TableRegion>}
    {!list.isPending && !list.error && <nav aria-label={t('allocation.pager')} className="flex items-center justify-end gap-3">
      <Button variant="outline" disabled={page <= 1} onClick={() => change('page', String(page - 1))}>{t('handoff.previous')}</Button>
      <span className="text-sm tabular-nums" aria-live="polite">{t('allocation.pageOf', { n: page, total: pages })}</span>
      <Button variant="outline" disabled={page * list.data.pageSize >= list.data.totalCount} onClick={() => change('page', String(page + 1))}>{t('handoff.next')}</Button>
    </nav>}
    {adding && options.data && <Proposal base={base} options={options.data} close={() => setAdding(false)} done={id => { setAdding(false); refresh(); open(id) }} />}
    {selected && <AllocationDetail base={base} id={selected} projectNumber={project.projectNumber} options={options.data}
      close={() => open(null)} refresh={refresh} />}
  </Page>
}

type LinkDraft = { key: string; workId: string; workDate: string; reviewHours: string }
type DayDraft = { key: string; workDate: string; hours: string }
const fieldset = 'space-y-3 rounded-lg border p-4'
const legend = 'px-1 text-sm font-semibold'
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
    submitLabel={t(existing ? 'allocation.saveChanges' : 'allocation.submitProposal')}
    payload={() => ({ ...(existing ? { rowVersion: existing.rowVersion, reason } : {}), personId, purpose, fromDate, throughDate,
      plannedHours: Number(plannedHours), days: days.map(d => ({ workDate: d.workDate, hours: Number(d.hours) })),
      links: links.map(l => ({ workType: purpose === 'Production' ? 'Task' : 'Review', workId: l.workId, workDate: l.workDate,
        reviewHours: purpose === 'Review' ? Number(l.reviewHours) : null })) })}>
    <div className="grid gap-3 sm:grid-cols-2">
      <SelectField label={t('allocation.purpose')} value={purpose} onChange={v => { setPurpose(v as 'Production' | 'Review'); setLinks([{ key: crypto.randomUUID(), workId: '', workDate: fromDate, reviewHours: '' }]) }}
        choices={[{ value: 'Production', label: t('allocation.purpose.Production') }, { value: 'Review', label: t('allocation.purpose.Review') }]} />
      <SelectField label={t('workload.person')} value={personId} onChange={v => { setPersonId(v); setLinks([{ key: crypto.randomUUID(), workId: '', workDate: fromDate, reviewHours: '' }]) }} choices={options.people.map(p => ({ value: p.id, label: p.displayName }))} />
    </div>
    {purpose === 'Review' && reviewOptions.error && <ErrorBanner error={reviewOptions.error} retry={() => reviewOptions.refetch()} />}
    {!choices.length && personId && <Notice title={t(purpose === 'Production' ? 'allocation.noTasks' : 'allocation.noReviews')} />}
    <div className="grid gap-3 sm:grid-cols-3"><Field label={t('allocation.from')} htmlFor="allocation-from"><Input id="allocation-from" type="date" required value={fromDate} onChange={e => setFromDate(e.target.value)} /></Field>
      <Field label={t('allocation.through')} htmlFor="allocation-through"><Input id="allocation-through" type="date" required min={fromDate} value={throughDate} onChange={e => setThroughDate(e.target.value)} /></Field>
      <Field label={t('allocation.plannedHours')} htmlFor="allocation-hours"><div className="flex items-center gap-2"><Input id="allocation-hours" type="number" required min="0.01" max="10000" step="0.01" value={plannedHours} onChange={e => setPlannedHours(e.target.value)} /><span aria-hidden className="text-sm text-muted-foreground">h</span></div></Field></div>
    <fieldset className={fieldset}><legend className={legend}>{t('allocation.linked')}</legend>{links.map((l, i) => <div className="space-y-3 rounded-md bg-muted p-3" key={l.key}>
      <SelectField label={`${t(purpose === 'Production' ? 'allocation.task' : 'allocation.reviewAssignment')} ${i + 1}`} value={l.workId} onChange={workId => updateLink(l.key, { workId })} choices={choices} />
      <div className="grid gap-3 sm:grid-cols-2"><Field label={`${t('allocation.linkDate')} ${i + 1}`} htmlFor={`allocation-work-${l.key}`}><Input id={`allocation-work-${l.key}`} type="date" required min={fromDate} max={throughDate} value={l.workDate} onChange={e => updateLink(l.key, { workDate: e.target.value })} /></Field>
        {purpose === 'Review' && <Field label={`${t('allocation.reviewEffort')} ${i + 1}`} htmlFor={`allocation-review-hours-${l.key}`}><Input id={`allocation-review-hours-${l.key}`} type="number" required min="0.01" max="10000" step="0.01" value={l.reviewHours} onChange={e => updateLink(l.key, { reviewHours: e.target.value })} /></Field>}</div>
      {links.length > 1 && <Button type="button" size="sm" variant="outline" onClick={() => setLinks(rows => rows.filter(row => row.key !== l.key))}>{t('allocation.removeLink')} {i + 1}</Button>}
    </div>)}<Button type="button" size="sm" variant="outline" disabled={links.length >= 500} onClick={() => setLinks(rows => [...rows, { key: crypto.randomUUID(), workId: '', workDate: fromDate, reviewHours: '' }])}><Plus className="size-4" />{t('allocation.addLink')}</Button></fieldset>
    <fieldset className={fieldset}><legend className={legend}>{t('allocation.daySplits')} <span className="font-normal text-muted-foreground">({t('common.optional')})</span></legend><p className="text-xs/[18px] text-muted-foreground">{t('allocation.daySplitsHint')}</p>{days.map((d, i) => <div className="grid gap-3 sm:grid-cols-[1fr_1fr_auto]" key={d.key}>
      <Field label={`${t('allocation.date')} ${i + 1}`} htmlFor={`allocation-day-${d.key}`}><Input id={`allocation-day-${d.key}`} type="date" required min={fromDate} max={throughDate} value={d.workDate} onChange={e => updateDay(d.key, { workDate: e.target.value })} /></Field>
      <Field label={`${t('allocation.hours')} ${i + 1}`} htmlFor={`allocation-day-hours-${d.key}`}><Input id={`allocation-day-hours-${d.key}`} type="number" required min="0" max="10000" step="0.01" value={d.hours} onChange={e => updateDay(d.key, { hours: e.target.value })} /></Field>
      <Button type="button" size="sm" variant="outline" className="self-end" onClick={() => setDays(rows => rows.filter(row => row.key !== d.key))}>{t('allocation.removeDay')} {i + 1}</Button>
    </div>)}<Button type="button" size="sm" variant="outline" disabled={days.length >= 366} onClick={() => setDays(rows => [...rows, { key: crypto.randomUUID(), workDate: fromDate, hours: '' }])}><Plus className="size-4" />{t('allocation.addDay')}</Button></fieldset>
    {existing && <Field label={t('common.reason')} htmlFor="allocation-reason" hint={t('common.reasonHint')}><Textarea id="allocation-reason" required minLength={5} value={reason} onChange={e => setReason(e.target.value)} /></Field>}
  </CommandForm>
}

const ACTION_BODY = { decline: 'allocation.declineBody', cancel: 'allocation.cancelBody', complete: 'allocation.completeBody' } as const

/** The allocation's detail panel (§13.0: a 560 px right panel on desktop, full width below), deep-linked by ?allocation=. */
function AllocationDetail({ base, id, projectNumber, options, close, refresh }: { base: string; id: string; projectNumber: string; options?: CoordOptions; close: () => void; refresh: () => void }) {
  const [preview, setPreview] = useState<Preview | null>(null), [action, setAction] = useState<'confirm' | 'decline' | 'cancel' | 'complete' | null>(null)
  const [editing, setEditing] = useState(false)
  const [error, setError] = useState<unknown>(null)
  const [previewing, setPreviewing] = useState(false)
  const detail = useQuery({ queryKey: ['allocation-detail', base, id], queryFn: () => get<Detail>(`${base}/${id}`) })
  const row = detail.data
  const done = () => { setAction(null); setPreview(null); setError(null); detail.refetch(); refresh() }
  const startConfirm = async () => { setError(null); setPreviewing(true); try { setPreview(await get<Preview>(`${base}/${id}/confirmation-preview`)); setAction('confirm') } catch (e) { setError(e) } finally { setPreviewing(false) } }
  if (editing && row && options) return <Proposal base={base} options={options} existing={row} close={() => setEditing(false)} done={() => { setEditing(false); done() }} />
  const live = row && ['Proposed', 'Confirmed'].includes(row.status)
  const over = preview?.days.filter(d => d.overByHours > 0) ?? []
  return <Sheet open onOpenChange={o => !o && close()}>
    <SheetContent side="right" className="w-full gap-0 overflow-y-auto p-0 sm:max-w-none xl:max-w-[560px] [&>button:last-child]:hidden">
      <div className="sticky top-0 z-10 flex items-center justify-between gap-2 border-b bg-card px-6 py-3">
        <SheetTitle className="text-sm font-semibold">{t('allocation.detail')}</SheetTitle>
        <Button variant="ghost" size="icon" onClick={close} aria-label={t('common.close')}><X className="size-5" /></Button>
      </div>
      <SheetDescription className="sr-only">{t('allocation.detailHint')}</SheetDescription>
      {detail.isPending ? <Loading rows={4} /> : detail.error ? <div className="p-6"><ErrorBanner error={detail.error} retry={() => detail.refetch()} /></div> : row && <div className="space-y-6 p-6">
        <div className="flex items-start gap-4">
          <Avatar id={row.personId} name={row.personName} className="size-12 text-base" />
          <div className="min-w-0 space-y-2">
            <h3 className="break-words text-2xl/8 font-semibold tracking-[-0.4px]">{row.personName ?? t('allocation.personUnavailable')}</h3>
            <div className="flex flex-wrap items-center gap-2"><Purpose purpose={row.purpose} /><AllocationStatus status={row.status} /></div>
          </div>
        </div>
        <p className="text-sm text-muted-foreground">{t(`allocation.statusHint.${row.status}`)}</p>
        {!row.personEligible && <div role="alert" className="rounded-md border border-warn/40 bg-warn-bg px-4 py-3 text-sm text-warn">
          <p><span aria-hidden>▲ </span>{eligibility(row.personState)}</p>
          {row.canManage && <p className="mt-1">{t('allocation.replacePersonHint')}</p>}
        </div>}
        <dl className="grid grid-cols-[minmax(8rem,auto)_1fr] gap-x-4 gap-y-2 text-sm">
          <dt className="text-muted-foreground">{t('allocation.dates')}</dt><dd className="tabular-nums">{fmtDate(row.fromDate)} – {fmtDate(row.throughDate)}</dd>
          <dt className="text-muted-foreground">{t('allocation.plannedHours')}</dt><dd className="font-semibold tabular-nums">{hours(row.plannedHours)}</dd>
          {row.confirmedAt && <><dt className="text-muted-foreground">{t('allocation.confirmation')}</dt>
            <dd>{['Confirmed', 'Completed'].includes(row.status) ? t('allocation.confirmedOn', { date: fmtDate(row.confirmedAt) }) : `${t('allocation.confirmationProvenance')} · ${fmtDate(row.confirmedAt)}`}</dd></>}
        </dl>
        {row.overCapacityWarningRecorded && <p className="rounded-md border border-warn/40 bg-warn-bg px-4 py-3 text-sm text-warn"><span aria-hidden>▲ </span>{t('allocation.overCapacityRecorded')}</p>}
        {row.days.length > 0 && <section className="space-y-2"><h4 className="text-base/6 font-semibold">{t('allocation.daySplits')}</h4>
          <table className="w-full text-sm"><thead className="text-muted-foreground"><tr><th scope="col" className="py-1.5 text-left font-medium">{t('allocation.date')}</th><th scope="col" className="py-1.5 text-right font-medium">{t('allocation.hours')}</th></tr></thead>
            <tbody>{row.days.map(d => <tr key={d.workDate} className="border-t"><td className="py-1.5 tabular-nums">{fmtDate(d.workDate)}</td><td className="py-1.5 text-right tabular-nums">{hours(d.hours)}</td></tr>)}</tbody></table></section>}
        <section className="space-y-2"><h4 className="text-base/6 font-semibold">{t('allocation.linked')}</h4>
          <ul className="divide-y rounded-md border text-sm">{row.links.map(l => <li key={`${l.workType}:${l.workId}:${l.workDate}`} className="flex flex-wrap items-center justify-between gap-x-3 gap-y-1 px-3 py-2">
            <span className="min-w-0 break-words">{l.workType === 'Review' ? <Link className="text-primary underline" to={`/projects/${projectNumber}/reviews${l.reviewPackageId ? `?panel=ReviewPackage:${l.reviewPackageId}` : ''}`}>{t('allocation.reviewAssignment')}</Link> :
              options ? <WorkLink options={options} type={l.workType} id={l.workId} number={projectNumber} /> :
                <Link className="text-primary underline" to={`/projects/${projectNumber}/tasks?panel=Task:${l.workId}`}>{t('allocation.openTask')}</Link>}</span>
            <span className="text-muted-foreground tabular-nums">{fmtDate(l.workDate)}{l.reviewHours != null && ` · ${hours(l.reviewHours)}`}</span>
          </li>)}</ul></section>
        {(row.canConfirm || row.canManage) && live ? <div className="flex flex-wrap gap-2 border-t pt-4">
          {row.status === 'Proposed' && row.canConfirm && <><Button onClick={startConfirm} disabled={previewing}>{previewing ? t('allocation.checking') : t('allocation.reviewCapacity')}</Button>
            <Button variant="outline" onClick={() => setAction('decline')}>{t('allocation.decline')}</Button></>}
          {row.canManage && options && <Button variant="outline" onClick={() => setEditing(true)}>{t('allocation.edit')}</Button>}
          {row.canManage && row.status === 'Confirmed' && <Button variant="outline" onClick={() => setAction('complete')}>{t('allocation.complete')}</Button>}
          {row.canManage && <Button variant="ghost" className="text-bad hover:text-bad" onClick={() => setAction('cancel')}>{t('allocation.cancel')}</Button>}
        </div> : live && <Notice icon={Lock} title={t('allocation.readOnlyTitle')}>{t('allocation.readOnly')}</Notice>}
        {/* The preview is refused when the viewer cannot see all of the person's work (FR-CAP-06); say what to do without
            naming or counting anything hidden. */}
        {error != null && ((error as ApiError).status === 403
          ? <Notice icon={Lock} title={t('allocation.previewDeniedTitle')}>{t('allocation.previewDenied')}</Notice>
          : <ErrorBanner error={error} />)}
      </div>}

      {action && row && <ConfirmDialog open onOpenChange={o => !o && setAction(null)} title={t(`allocation.${action}`)} destructive={action === 'cancel'}
        className={action === 'confirm' ? 'sm:max-w-2xl' : undefined}
        confirmLabel={t(`allocation.${action}`)}
        body={action === 'confirm' ? t('allocation.confirmHint') : t(ACTION_BODY[action])}
        reason={action === 'confirm' ? over.length > 0 : true}
        onConfirm={async reason => {
          if (action === 'confirm' && !preview) return
          const payload = action === 'confirm' ? { requestId: crypto.randomUUID(), rowVersion: preview!.rowVersion,
            dateVersions: preview!.days.map(d => ({ workDate: d.date, rowVersion: d.dateVersion })), overCapacityReason: reason || null } :
            { requestId: crypto.randomUUID(), rowVersion: row.rowVersion, reason }
          await post(`${base}/${id}/${action}`, payload); toast.success(t('common.saved')); done()
        }}>
        {action === 'confirm' && preview && <>
          {over.length > 0
            ? <p role="alert" className="flex gap-2 rounded-md border border-bad/30 bg-bad-bg px-4 py-3 text-sm text-bad"><AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden />{t('allocation.overDays', { n: over.length, h: hours(over.reduce((s, d) => s + d.overByHours, 0)) })}</p>
            : <p className="flex gap-2 rounded-md border border-ok/30 bg-ok-bg px-4 py-3 text-sm text-ok"><CheckCircle2 className="mt-0.5 size-4 shrink-0" aria-hidden />{t('allocation.withinCapacity')}</p>}
          {/* eslint-disable-next-line jsx-a11y/no-noninteractive-tabindex -- axe scrollable-region-focusable: keyboard users scroll the dated rows */}
          <div className="scroll-region max-h-64 overflow-auto rounded-md border text-sm" tabIndex={0} role="region" aria-label={t('allocation.reviewCapacity')}><table className="w-full">
            <thead className="sticky top-0 bg-muted text-muted-foreground"><tr>
              <th scope="col" className="whitespace-nowrap px-3 py-2 text-left font-medium">{t('allocation.date')}</th>
              {[t('allocation.available'), t('allocation.existing'), t('allocation.resulting'), t('allocation.over')].map(h => <th scope="col" key={h} className="whitespace-nowrap px-3 py-2 text-right font-medium">{h}</th>)}
            </tr></thead>
            <tbody>{preview.days.map(d => <tr key={d.date} className={cn('border-t', d.overByHours > 0 && 'bg-bad-bg/60')}><td className="whitespace-nowrap px-3 py-2 tabular-nums">{fmtDate(d.date)}</td>
              <td className="px-3 py-2 text-right tabular-nums">{hours(d.availableHours)}</td><td className="px-3 py-2 text-right tabular-nums">{hours(d.confirmedHours)}</td>
              <td className="px-3 py-2 text-right tabular-nums">{hours(d.resultingHours)}</td>
              <td className="px-3 py-2 text-right tabular-nums">{d.overByHours > 0 ? <strong className="text-bad"><span aria-hidden>■ </span>{hours(d.overByHours)}</strong> : hours(0)}</td></tr>)}</tbody>
          </table></div>
        </>}
      </ConfirmDialog>}
    </SheetContent>
  </Sheet>
}
