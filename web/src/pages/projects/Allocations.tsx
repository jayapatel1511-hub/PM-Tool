import type { components, paths } from '@/gen/api'
import { usePageMode } from '@/hooks/page-mode'
import { UrlSearchInput } from '@/components/hub/url-search'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { AlertTriangle, CheckCircle2, Ellipsis, Lock, Monitor, Plus, X } from 'lucide-react'
import { useEffect, useRef, useState, type ReactNode } from 'react'
import { Link, useMatch, useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { ActiveFilters, ConfirmDialog, DesktopOnly, Empty, ErrorBanner, Field, FilterBar, Loading, Missing, Notice, Page, PageHelp, Segmented, Spinner, TableRegion, selectCls, tdCls as td, thCls as th } from '@/components/hub/common'
import { ExportMenu } from '@/components/hub/export'
import { FieldRow, HistoryList, TabBar } from '@/components/hub/fields'
import { useItemPanel, type PanelProps } from '@/components/hub/panel-host'
import { Avatar } from '@/components/hub/people'
import { Pill, type Tone } from '@/components/hub/pills'
import { DataTable, FieldGroup, PanelHead, SELECTED_ROW, TITLE_LINK } from '@/components/hub/registers'
import { useTable, type Column } from '@/components/hub/table'
import { ViewMenu } from '@/components/hub/views'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { Sheet, SheetContent, SheetDescription, SheetTitle } from '@/components/ui/sheet'
import { Textarea } from '@/components/ui/textarea'
import { useProject, useReference } from '@/hooks/data'
import { useFlag } from '@/lib/auth'
import { ApiError, get, post, qs, typedRequest } from '@/lib/api'
import { useOffer, type Offer } from '@/lib/auth'
import { fmtDate, hours, today, loadBandText } from '@/lib/format'
import { runtimeText, t, tv, type TextKey } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { invalidateChange } from '@/lib/keys'
import { CommandForm, SelectField, type CoordOptions, WorkLink } from './CoordinationForms'
import { useCurrentProject } from './ProjectLayout'
import { DraftNotice } from '@/components/hub/drafts'
import { useSafeForm } from '@/hooks/drafts'

type Allocation = { id: string; personId: string; personName: string | null; purpose: string; fromDate: string; throughDate: string; plannedHours: number; status: string; rowVersion: number; personState: 'Eligible' | 'Inactive' | 'Removed' | 'Missing' }
type AllocationPage = { items: Allocation[]; page: number; pageSize: number; totalCount: number }
type Detail = Allocation & { personEligible: boolean; confirmedBy?: string; confirmedAt?: string; links: { workType: string; workId: string; workDate: string; reviewHours?: number; reviewPackageId?: string }[]; days: { workDate: string; hours: number }[]; overCapacityWarningRecorded: boolean; canConfirm: boolean; canManage: boolean }
type Preview = { id: string; rowVersion: number; days: { date: string; availableHours: number; confirmedHours: number; proposedHours: number; resultingHours: number; overByHours: number; dateVersion: number }[] }
type AllocationPreview = Extract<paths['/api/v1/projects/{projectId}/allocations']['post']['responses'][200]['content']['application/json'], { preview: boolean }>
type ReviewOption = { id: string; reviewerId: string; dueDate: string; packageId: string; packageKey: string; packageTitle: string }
/** Unfilled project demand (§40, FR-ROLE-01): filled by proposing a normal allocation that names it (D1). */
type OpenRole = components['schemas']['OpenRoleRow']
type Fill = { openRoleId: string; roleLabel: string; fromDate: string; throughDate: string; plannedHours: number }

/** The preview's band and indicator codes (§45.4, FR-VER-12) to text. */
const PREVIEW_INDICATOR: Record<string, TextKey> = { overCapacity: 'preview.overCapacity' }
const previewIndicators = (codes: string[]) => codes.map(code => t(PREVIEW_INDICATOR[code] ?? 'common.dash')).join(' · ')
const ROLE_TONE: Record<string, Tone> = { Open: 'work', Filled: 'done', Cancelled: 'idle' }

/** Approval status (FR-CAP-01) with the §13.0 symbol: a request awaiting the supervisor, an accepted commitment, or closed.
 *  It is an approval state only; the column says so, so "Confirmed" is never read as a confidence or visibility level. */
const STATUS_TONE: Record<string, Tone> = { Proposed: 'work', Confirmed: 'done', Completed: 'done', Declined: 'idle', Cancelled: 'idle' }
const AllocationStatus = ({ status }: { status: string }) => <Pill tone={STATUS_TONE[status] ?? 'idle'}>{tv(status)}</Pill>

/** Purpose is a category (identity tint), not a status. */
const PURPOSE_ACCENT: Record<string, string> = { Production: 'blue', Review: 'lavender' }
const Purpose = ({ purpose }: { purpose: string }) => (
  <span data-accent={PURPOSE_ACCENT[purpose] ?? 'blue'} className="inline-flex items-center whitespace-nowrap rounded-md bg-(--acc-bg) px-2 py-0.5 text-xs/[18px] font-medium text-(--acc-fg)">{runtimeText(`allocation.purpose.${purpose}`)}</span>
)
const eligibility = (state: Allocation['personState']) => t(state === 'Inactive' ? 'allocation.personInactiveWarning' : state === 'Removed' ? 'allocation.personRemovedWarning' : 'allocation.personMissingWarning')

export function AllocationsTab() {
  // §41 R5: a short scope, with the fuller description behind the help button.
  const header = { title: t('allocation.title'), subtitle: <>{t('allocation.scope')}<PageHelp>{t('allocation.subtitle')} {t('allocation.emptyReadOnly')}</PageHelp></> }
  return <DesktopOnly notice={<Page {...header}><Notice icon={Monitor} title={t('app.phoneNotice')}>{t('allocation.phoneHint')}</Notice></Page>}><Allocations {...header} /></DesktopOnly>
}

function Allocations(header: { title: string; subtitle: ReactNode }) {
  const project = useCurrentProject(), qc = useQueryClient()
  const [sp, setSp] = useSearchParams(), [adding, setAdding] = useState(false)
  const selected = sp.get('allocation')
  const page = Math.max(1, Number(sp.get('page')) || 1)
  const filters = Object.fromEntries(['q', 'personId', 'purpose', 'status', 'from', 'to'].map(k => [k, sp.get(k) ?? '']))
  const filtered = Object.values(filters).some(Boolean)
  const open = (id: string | null) => { const next = new URLSearchParams(sp); if (id) next.set('allocation', id); else next.delete('allocation'); setSp(next) }
  const base = `projects/${project.id}/allocations`
  const list = useQuery({ queryKey: ['allocations', project.id, filters, page], queryFn: () => get<AllocationPage>(`${base}${qs({ ...filters, page })}`) })
  const options = useQuery({ queryKey: ['coord-options', project.id], queryFn: () => typedRequest('get', '/api/v1/projects/{projectId}/changes/options', { params: { projectId: project.id } }) })
  const proposal = useOffer()(project.permissions.proposeAllocation, project.status) // FR-FIG-08 decision 1
  // §47.5 FR-REC-12, behind flag.record_engine_additions: the open roles as CSV or Excel, after the page's own entries.
  const roleExport = useFlag('record_engine_additions') ? [{ path: 'lists/open-roles/export', params: { projectId: project.id }, name: `${project.projectNumber}-open-roles`, label: t('openRole.tab') }] : undefined
  const [roleForm, setRoleForm] = useState<OpenRole | 'new' | null>(null), [filling, setFilling] = useState<Fill | null>(null), [cancelling, setCancelling] = useState<OpenRole | null>(null)
  const refresh = () => { qc.invalidateQueries({ queryKey: ['allocations', project.id] }); qc.invalidateQueries({ queryKey: ['workload'] }) }
  const change = (key: string, value: string) => { const next = new URLSearchParams(sp); if (value) next.set(key, value); else next.delete(key); if (key !== 'page' && key !== 'allocation') next.delete('page'); setSp(next) }
  const clear = () => { const next = new URLSearchParams(sp); for (const k of Object.keys(filters)) next.delete(k); next.delete('page'); setSp(next) }
  const pages = list.data ? Math.max(1, Math.ceil(list.data.totalCount / list.data.pageSize)) : 1
  const view = usePageMode('tab', ['roles'], 'allocations')
  const roles = useQuery({ queryKey: ['allocations', project.id, 'open-roles'], queryFn: () => typedRequest('get', '/api/v1/projects/{projectId}/open-roles', { params: { projectId: project.id } }) })
  // A Workload Fill link (?fill=) opens the proposal once per link; closing it must not reopen it while the URL catches up.
  const handledFill = useRef<string | null>(null)
  useEffect(() => {
    const id = sp.get('fill')
    if (!id) { handledFill.current = null; return }
    const role = handledFill.current === id ? undefined : roles.data?.find(r => r.id === id && r.status === 'Open')
    if (role && proposal.shown) {
      handledFill.current = id
      setFilling({ openRoleId: role.id, roleLabel: role.roleLabel, fromDate: role.fromDate, throughDate: role.throughDate, plannedHours: role.plannedHours })
    }
  }, [roles.data, proposal.shown, sp])
  const openRoles = roles.data?.filter(r => r.status === 'Open').length ?? 0
  const complete = project.permissions.needsReason // P-05, FR-FIG-11: changes to a Complete project are corrections with a reason (A6)
  const propose = view === 'roles'
    ? <Button {...proposal.props} onClick={() => setRoleForm('new')}><Plus className="size-4" />{t('openRole.new')}</Button>
    : <Button {...proposal.props} onClick={() => setAdding(true)}><Plus className="size-4" />{t('allocation.new')}</Button>
  // Active filters as removable chips (§41 R1); the fields themselves open from the Filters button.
  const tokens = ([
    ['q', t('common.search'), filters.q],
    ['personId', t('workload.person'), options.data?.people.find(p => p.id === filters.personId)?.displayName ?? t('task.filterSelected')],
    ['purpose', t('allocation.purpose'), filters.purpose && runtimeText(`allocation.purpose.${filters.purpose}`)],
    ['status', t('allocation.status'), filters.status && tv(filters.status)],
    ['from', t('allocation.from'), filters.from && fmtDate(filters.from)],
    ['to', t('allocation.through'), filters.to && fmtDate(filters.to)],
  ] as const).filter(([k]) => filters[k]).map(([key, label, value]) => ({ key, label, value: value || t('common.dash') }))
  // The list's columns, fixed and unsorted as the hand-rolled table was (§47.7): the server filters, orders and pages the rows.
  const table = useTable<Allocation>('hub.allocationColumns', [
    { id: 'person', label: t('workload.person'), fixed: true, className: 'align-top', cell: a => <div className="flex items-start gap-2.5">
      <Avatar id={a.personId} name={a.personName} />
      <div className="min-w-0">
        <button className="break-words text-left font-semibold text-primary underline-offset-4 hover:underline" aria-current={selected === a.id || undefined} onClick={() => open(a.id)}>{a.personName ?? t('allocation.personUnavailable')}</button>
        {a.personState !== 'Eligible' && ['Proposed', 'Confirmed'].includes(a.status) && <p className="mt-1 text-xs/[18px] text-warn"><span aria-hidden>▲ </span>{eligibility(a.personState)}</p>}
      </div>
    </div> },
    { id: 'purpose', label: t('allocation.purpose'), fixed: true, className: 'align-top', cell: a => <Purpose purpose={a.purpose} /> },
    { id: 'dates', label: t('allocation.dates'), fixed: true, className: 'whitespace-nowrap align-top tabular-nums', cell: a => <>{fmtDate(a.fromDate)} – {fmtDate(a.throughDate)}</> },
    { id: 'hours', label: t('allocation.plannedHours'), fixed: true, className: 'text-right align-top font-medium tabular-nums', cell: a => hours(a.plannedHours) },
    { id: 'status', label: t('allocation.status'), fixed: true, className: 'align-top', cell: a => <AllocationStatus status={a.status} /> },
  ], list.data?.items ?? [], a => [a.fromDate, a.personName])
  return <Page {...header} actions={<><ViewMenu listType="allocations" projectId={project.id} panelParam="allocation" /><ExportMenu path={`${base}/export`} params={filters} name={`${project.projectNumber}-allocations`} extra={roleExport} />{proposal.shown && propose}</>}>
    <FilterBar activeCount={tokens.length}
      search={view === 'allocations' && <Field label={t('common.search')} htmlFor="allocation-search"><UrlSearchInput id="allocation-search" placeholder={t('allocation.search')} value={filters.q} onValueChange={value => change('q', value)} /></Field>}
      active={view === 'allocations' && <ActiveFilters tokens={tokens} onRemove={k => change(k, '')} onClear={clear} />}
      actions={<Segmented label={t('openRole.view')} value={view} onChange={v => change('tab', v === 'roles' ? 'roles' : '')}
        options={[{ value: 'allocations', label: t('allocation.title') }, { value: 'roles', label: openRoles ? t('openRole.tabCount', { n: openRoles }) : t('openRole.tab') }]} />}>
      {view === 'allocations' && <div className="flex flex-wrap items-end gap-3">
        <div className="w-full sm:w-48"><SelectField label={t('workload.person')} value={filters.personId} onChange={v => change('personId', v)} required={false} choices={options.data?.people.map(p => ({ value: p.id, label: p.displayName })) ?? []} /></div>
        <div className="w-full sm:w-40"><SelectField label={t('allocation.purpose')} value={filters.purpose} onChange={v => change('purpose', v)} required={false} choices={['Production', 'Review'].map(v => ({ value: v, label: runtimeText(`allocation.purpose.${v}`) }))} /></div>
        <div className="w-full sm:w-44"><SelectField label={t('allocation.status')} value={filters.status} onChange={v => change('status', v)} required={false} choices={['Proposed', 'Confirmed', 'Declined', 'Cancelled', 'Completed'].map(v => ({ value: v, label: tv(v) }))} /></div>
        <Field label={t('allocation.from')} htmlFor="allocation-filter-from" className="w-full sm:w-40"><Input id="allocation-filter-from" type="date" value={filters.from} onChange={e => change('from', e.target.value)} /></Field>
        <Field label={t('allocation.through')} htmlFor="allocation-filter-to" className="w-full sm:w-40"><Input id="allocation-filter-to" type="date" value={filters.to} onChange={e => change('to', e.target.value)} /></Field>
      </div>}
    </FilterBar>
    {options.error && <ErrorBanner error={options.error} retry={() => options.refetch()} />}
    {view === 'roles' ? <OpenRoles roles={roles} base={base} proposal={proposal} canFill={proposal.shown && !!options.data}
      onAdd={() => setRoleForm('new')} onEdit={setRoleForm} onCancel={setCancelling} onOpenAllocation={open}
      onFill={r => setFilling({ openRoleId: r.id, roleLabel: r.roleLabel, fromDate: r.fromDate, throughDate: r.throughDate, plannedHours: r.plannedHours })} /> : <>
    {list.error ? <ErrorBanner error={list.error} retry={() => list.refetch()} />
      : <DataTable table={table} rows={table.sorted} loading={list.isPending} wide caption={t('allocation.title')} current={a => selected === a.id}
        empty={filtered
          ? <Empty title={t('allocation.emptyFilteredTitle')} action={<Button variant="outline" onClick={clear}>{t('filters.clear')}</Button>}>{t('allocation.emptyFiltered')}</Empty>
          : <Empty title={t('allocation.empty')} action={proposal.shown && <Button variant="outline" {...proposal.props} onClick={() => setAdding(true)}><Plus className="size-4" />{t('allocation.new')}</Button>}>{proposal.shown && t('allocation.emptyHint')}</Empty>} />}
    {!list.isPending && !list.error && pages > 1 && <nav aria-label={t('allocation.pager')} className="flex items-center justify-end gap-3">
      <Button variant="outline" disabled={page <= 1} onClick={() => change('page', String(page - 1))}>{t('handoff.previous')}</Button>
      <span className="text-sm tabular-nums" aria-live="polite">{t('allocation.pageOf', { n: page, total: pages })}</span>
      <Button variant="outline" disabled={page * list.data.pageSize >= list.data.totalCount} onClick={() => change('page', String(page + 1))}>{t('handoff.next')}</Button>
    </nav>}
    </>}
    {adding && options.data && <Proposal projectId={project.id} base={base} options={options.data} complete={complete} close={() => setAdding(false)} done={id => { setAdding(false); refresh(); open(id) }} />}
    {filling && options.data && <Proposal projectId={project.id} base={base} options={options.data} complete={complete} fill={filling} close={() => { setFilling(null); if (sp.has('fill')) { const next = new URLSearchParams(sp); next.delete('fill'); setSp(next, { replace: true }) } }}
      done={id => { setFilling(null); refresh(); const next = new URLSearchParams(sp); next.delete('fill'); next.set('allocation', id); setSp(next) }} />}
    {roleForm && <RoleDialog projectId={project.id} disciplines={project.disciplines} role={roleForm === 'new' ? undefined : roleForm} complete={complete}
      onClose={() => setRoleForm(null)} onDone={() => { setRoleForm(null); invalidateChange(qc, 'OpenRole', project.id) }} />}
    {cancelling && <ConfirmDialog open onOpenChange={o => !o && setCancelling(null)} title={t('openRole.cancelTitle', { role: cancelling.roleLabel })} destructive
      body={t('openRole.cancelBody')} confirmLabel={t('openRole.cancel')} reason={complete ? true : 'optional'}
      onConfirm={async reason => { await typedRequest('post', '/api/v1/open-roles/{id}/cancel', { params: { id: cancelling.id }, body: { rowVersion: cancelling.rowVersion, reason: reason || null } }); toast.success(t('common.saved')); setCancelling(null); invalidateChange(qc, 'OpenRole', project.id) }} />}
    {selected && <AllocationDetail projectId={project.id} base={base} id={selected} projectNumber={project.projectNumber} options={options.data}
      close={() => open(null)} refresh={refresh} />}
  </Page>
}

type LinkDraft = { key: string; workId: string; workDate: string; reviewHours: string }
type DayDraft = { key: string; workDate: string; hours: string }
const fieldset = 'space-y-3 rounded-lg border p-4'
const legend = 'px-1 text-sm font-semibold'
/** Propose or edit an allocation. With `fill`, the same form fills an open role: prefilled with the role's dates and hours,
 *  sending `openRoleId` so the role is marked Filled in the same transaction (FR-ROLE-03, AC-ROLE-03). */
function Proposal({ projectId, base, options, close, done, existing, fill, complete }: { projectId: string; base: string; options: CoordOptions; close: () => void; done: (id: string) => void; existing?: Detail; fill?: Fill; complete?: boolean }) {
  const previewEnabled = useFlag('allocation_preview')
  const [purpose, setPurpose] = useState<'Production' | 'Review'>((existing?.purpose as 'Production' | 'Review') ?? 'Production')
  const start = existing?.fromDate ?? fill?.fromDate ?? today()
  const [personId, setPersonId] = useState(existing?.personId ?? ''), [fromDate, setFromDate] = useState(start), [throughDate, setThroughDate] = useState(existing?.throughDate ?? fill?.throughDate ?? today())
  const [plannedHours, setPlannedHours] = useState(existing ? String(existing.plannedHours) : fill ? String(fill.plannedHours) : '')
  const [links, setLinks] = useState<LinkDraft[]>(() => existing?.links.map(l => ({ key: crypto.randomUUID(), workId: l.workId, workDate: l.workDate, reviewHours: l.reviewHours == null ? '' : String(l.reviewHours) })) ??
    [{ key: crypto.randomUUID(), workId: '', workDate: start, reviewHours: '' }])
  const [days, setDays] = useState<DayDraft[]>(() => existing?.days.map(d => ({ key: crypto.randomUUID(), workDate: d.workDate, hours: String(d.hours) })) ?? [])
  const [reason, setReason] = useState('')
  const [allocationPreview, setAllocationPreview] = useState<AllocationPreview | null>(null), [previewError, setPreviewError] = useState<unknown>(null)
  const reviewOptions = useQuery({ queryKey: ['allocation-review-options', base], queryFn: () => get<ReviewOption[]>(`${base}/review-options`) })
  const tasks = options.tasks.filter(w => w.ownerId === personId && !['Complete', 'Cancelled', 'On Hold'].includes(w.status))
  const reviews = (reviewOptions.data ?? []).filter(r => r.reviewerId === personId)
  const choices = purpose === 'Production' ? tasks.map(w => ({ value: w.id, label: `${w.key} · ${w.name}` })) :
    reviews.map(r => ({ value: r.id, label: `${r.packageKey} · ${r.packageTitle} · ${fmtDate(r.dueDate)}` }))
  const updateLink = (key: string, change: Partial<LinkDraft>) => setLinks(rows => rows.map(row => row.key === key ? { ...row, ...change } : row))
  const updateDay = (key: string, change: Partial<DayDraft>) => setDays(rows => rows.map(row => row.key === key ? { ...row, ...change } : row))
  const needsReason = !!existing || !!complete
  const previewBody = () => ({ ...(existing ? { rowVersion: existing.rowVersion } : {}), ...(needsReason ? { reason } : {}), ...(fill ? { openRoleId: fill.openRoleId } : {}), requestId: crypto.randomUUID(), personId, purpose, fromDate, throughDate,
    plannedHours: Number(plannedHours), days: days.map(d => ({ workDate: d.workDate, hours: Number(d.hours) })), links: links.map(l => ({ workType: purpose === 'Production' ? 'Task' : 'Review', workId: l.workId, workDate: l.workDate, reviewHours: purpose === 'Review' ? Number(l.reviewHours) : null })) })
  const previewReady = previewEnabled && !!personId && /^\d{4}-\d{2}-\d{2}$/.test(fromDate) && /^\d{4}-\d{2}-\d{2}$/.test(throughDate) && fromDate <= throughDate && Number(plannedHours) > 0 && links.every(l => !!l.workId && /^\d{4}-\d{2}-\d{2}$/.test(l.workDate) && (purpose !== 'Review' || Number(l.reviewHours) > 0)) && days.every(d => /^\d{4}-\d{2}-\d{2}$/.test(d.workDate) && Number(d.hours) >= 0) && (!needsReason || reason.trim().length >= 5)
  useEffect(() => {
    if (!previewReady) { setAllocationPreview(null); setPreviewError(null); return }
    let cancelled = false
    const timer = window.setTimeout(async () => {
      try { setPreviewError(null); const body = previewBody(); const result = existing
          ? await typedRequest('post', '/api/v1/projects/{projectId}/allocations/{id}/edit', { params: { projectId, id: existing.id }, query: { preview: true }, body: { ...body, rowVersion: existing.rowVersion, reason } })
          : await typedRequest('post', '/api/v1/projects/{projectId}/allocations', { params: { projectId }, query: { preview: true }, body: { ...body, reason: needsReason ? reason : null, openRoleId: fill?.openRoleId ?? null } });
        if (!('preview' in result)) throw new Error(t('app.error')); if (!cancelled) setAllocationPreview(result) }
      catch (e) { if (!cancelled) { setAllocationPreview(null); setPreviewError(e) } }
    }, 300)
    return () => { cancelled = true; window.clearTimeout(timer) }
  // eslint-disable-next-line react-hooks/exhaustive-deps -- fields are the explicit preview inputs
  }, [previewReady, projectId, base, existing?.id, personId, purpose, fromDate, throughDate, plannedHours, reason, fill?.openRoleId, JSON.stringify(links), JSON.stringify(days)])
  return <CommandForm path={existing ? `${base}/${existing.id}/edit` : base} title={fill ? t('openRole.fillTitle', { role: fill.roleLabel }) : t(existing ? 'allocation.edit' : 'allocation.new')}
    hint={fill ? t('openRole.fillHint', { role: fill.roleLabel, from: fmtDate(fill.fromDate), through: fmtDate(fill.throughDate) }) : t(existing?.status === 'Confirmed' ? 'allocation.editConfirmedHint' : 'allocation.proposalHint')} onClose={close} onDone={done}
    submitLabel={t(existing ? 'allocation.saveChanges' : 'allocation.submitProposal')}
    payload={() => ({ ...(existing ? { rowVersion: existing.rowVersion } : {}), ...(needsReason ? { reason } : {}), ...(fill ? { openRoleId: fill.openRoleId } : {}), personId, purpose, fromDate, throughDate,
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
    <div className="grid gap-3 sm:grid-cols-3"><Field label={t('allocation.from')} htmlFor="allocation-from"><Input id="allocation-from" type="date" required min={fill?.fromDate} max={fill?.throughDate} value={fromDate} onChange={e => setFromDate(e.target.value)} /></Field>
      <Field label={t('allocation.through')} htmlFor="allocation-through"><Input id="allocation-through" type="date" required min={fromDate} max={fill?.throughDate} value={throughDate} onChange={e => setThroughDate(e.target.value)} /></Field>
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
    {needsReason && <Field label={t('common.reason')} htmlFor="allocation-reason" hint={t(existing ? 'common.reasonHint' : 'openRole.correctionHint')}><Textarea id="allocation-reason" required minLength={5} value={reason} onChange={e => setReason(e.target.value)} /></Field>}
    {previewEnabled && (previewError ? <ErrorBanner error={previewError} /> : allocationPreview && <section className="rounded-md border bg-muted px-4 py-3 text-sm" aria-live="polite"><h3 className="font-semibold">{t('preview.title')}</h3><ul className="mt-2 space-y-1">{allocationPreview.weeks.map(w => <li key={`${w.personId}:${w.week}`} className="flex flex-wrap justify-between gap-2"><span>{t('preview.after', { planned: w.planned, capacity: w.capacity, band: loadBandText(w.band) })}</span>{w.indicators.length > 0 && <span className="text-muted-foreground">{previewIndicators(w.indicators)}</span>}</li>)}</ul></section>)}
  </CommandForm>
}

const ACTION_BODY = { decline: 'allocation.declineBody', cancel: 'allocation.cancelBody', complete: 'allocation.completeBody' } as const

/** The allocation's detail panel (§13.0: a 560 px right panel on desktop, full width below), deep-linked by ?allocation=. */
function AllocationDetail({ projectId, base, id, projectNumber, options, close, refresh }: { projectId: string; base: string; id: string; projectNumber: string; options?: CoordOptions; close: () => void; refresh: () => void }) {
  const small = useFlag('small_ux')
  const [preview, setPreview] = useState<Preview | null>(null), [action, setAction] = useState<'confirm' | 'decline' | 'cancel' | 'complete' | null>(null)
  const [allocationPreview, setAllocationPreview] = useState<AllocationPreview | null>(null)
  const [confirmReason, setConfirmReason] = useState(''), [confirmPreviewError, setConfirmPreviewError] = useState<unknown>(null)
  const [confirmPreviewPending, setConfirmPreviewPending] = useState(false)
  const [editing, setEditing] = useState(false)
  const [error, setError] = useState<unknown>(null)
  const [previewing, setPreviewing] = useState(false)
  const previewEnabled = useFlag('allocation_preview')
  const detail = useQuery({ queryKey: ['allocation-detail', base, id], queryFn: () => get<Detail>(`${base}/${id}`) })
  const row = detail.data
  const done = () => { setAction(null); setPreview(null); setError(null); detail.refetch(); refresh() }
  const startConfirm = async () => { setError(null); setPreviewing(true); setConfirmReason(''); setAllocationPreview(null); try { setPreview(await get<Preview>(`${base}/${id}/confirmation-preview`)); setAction('confirm') } catch (e) { setError(e) } finally { setPreviewing(false) } }
  const needsConfirmReason = preview?.days.some(d => d.overByHours > 0) ?? false
  useEffect(() => {
    setAllocationPreview(null); setConfirmPreviewError(null)
    if (!previewEnabled || action !== 'confirm' || !preview || needsConfirmReason && confirmReason.trim().length < 5) { setConfirmPreviewPending(false); return }
    let cancelled = false; setConfirmPreviewPending(true)
    const timer = window.setTimeout(async () => {
      try {
        const result = await typedRequest('post', '/api/v1/projects/{projectId}/allocations/{id}/confirm', { params: { projectId, id }, query: { preview: true }, body: { requestId: crypto.randomUUID(), rowVersion: preview.rowVersion,
          dateVersions: preview.days.map(d => ({ workDate: d.date, rowVersion: d.dateVersion })), overCapacityReason: confirmReason.trim() || null } })
        if (!('preview' in result)) throw new Error(t('app.error'))
        if (!cancelled) setAllocationPreview(result)
      } catch (e) { if (!cancelled) setConfirmPreviewError(e) }
      finally { if (!cancelled) setConfirmPreviewPending(false) }
    }, 300)
    return () => { cancelled = true; window.clearTimeout(timer) }
  }, [previewEnabled, action, preview, needsConfirmReason, confirmReason, projectId, base, id])
  if (editing && row && options) return <Proposal projectId={projectId} base={base} options={options} existing={row} close={() => setEditing(false)} done={() => { setEditing(false); done() }} />
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
        <p className="text-sm text-muted-foreground">{runtimeText(`allocation.statusHint.${row.status}`)}</p>
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

      {action && row && <ConfirmDialog open onOpenChange={o => !o && setAction(null)} title={runtimeText(`allocation.${action}`)} destructive={action === 'cancel'}
        className={action === 'confirm' ? 'sm:max-w-2xl' : undefined}
        confirmLabel={runtimeText(`allocation.${action}`)}
        body={action === 'confirm' ? t('allocation.confirmHint') : t(ACTION_BODY[action])}
        reason={action === 'confirm' ? !previewEnabled && over.length > 0 : true}
        busy={action === 'confirm' && previewEnabled && (confirmPreviewPending || !allocationPreview)}
        onConfirm={async reason => {
          if (action === 'confirm' && !preview) return
          if (action === 'confirm' && previewEnabled) { if (!allocationPreview) return; reason = confirmReason.trim() }
          const payload = action === 'confirm' ? { requestId: crypto.randomUUID(), rowVersion: preview!.rowVersion,
            dateVersions: preview!.days.map(d => ({ workDate: d.date, rowVersion: d.dateVersion })), overCapacityReason: reason || null } :
            { requestId: crypto.randomUUID(), rowVersion: row.rowVersion, reason }
          try { await post(`${base}/${id}/${action}`, payload); toast.success(t('common.saved')); done() } catch (e) { setError(e); if ((e as ApiError).status === 409) { setAction(null); await detail.refetch() } }
        }}>
        {action === 'confirm' && preview && <>
          {previewEnabled && <>
            <Field label={t('common.reason')} htmlFor="allocation-confirm-reason" hint={needsConfirmReason ? undefined : t('common.optional')}><Textarea id="allocation-confirm-reason" value={confirmReason} onChange={e => setConfirmReason(e.target.value)} required={needsConfirmReason} minLength={needsConfirmReason ? 5 : undefined} /></Field>
            {confirmPreviewError != null && <ErrorBanner error={confirmPreviewError} />}
            {allocationPreview && <ul className="rounded-md border bg-muted px-4 py-2 text-sm" aria-live="polite">{allocationPreview.weeks.map(w => <li key={`${w.personId}:${w.week}`}>{fmtDate(w.week)} · {t('preview.after', { planned: w.planned, capacity: w.capacity, band: loadBandText(w.band) })}</li>)}</ul>}
          </>}
          {over.length > 0
            ? <p role="alert" className="flex gap-2 rounded-md border border-bad/30 bg-bad-bg px-4 py-3 text-sm text-bad"><AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden />{t(small ? 'allocation.overDaysOf' : 'allocation.overDays', { n: over.length, m: preview.days.length, h: hours(over.reduce((s, d) => s + d.overByHours, 0)) })}</p>
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

/** The project's open roles (FR-ROLE-01..04): demand before a person is named. Fill proposes a normal allocation for it.
 *  Behind flag.record_engine_additions (§47.5 FR-REC-12) the headers sort and the role label opens the read-only panel. */
function OpenRoles({ roles, base, proposal, canFill, onAdd, onEdit, onCancel, onFill, onOpenAllocation }: {
  roles: ReturnType<typeof useQuery<OpenRole[]>>; base: string; proposal: Offer; canFill: boolean
  onAdd: () => void; onEdit: (r: OpenRole) => void; onCancel: (r: OpenRole) => void; onFill: (r: OpenRole) => void; onOpenAllocation: (id: string) => void
}) {
  const additions = useFlag('record_engine_additions'), openPanel = useItemPanel(), [sp] = useSearchParams()
  const current = (r: OpenRole) => additions && sp.get('panel') === `OpenRole:${r.id}`
  const columns: Column<OpenRole>[] = [
    { id: 'role', label: t('openRole.role'), fixed: true, sort: r => r.roleLabel.toLowerCase(), cell: r => <>
      {additions ? <button type="button" className={cn(TITLE_LINK, 'block font-semibold')} aria-current={current(r) || undefined} onClick={() => openPanel('OpenRole', r.id)}>{r.roleLabel}</button>
        : <div className="break-words font-semibold">{r.roleLabel}</div>}
      {(r.disciplineName || r.note) && <div className="text-xs/[18px] text-muted-foreground">{[r.disciplineName, r.note].filter(Boolean).join(' · ')}</div>}</> },
    { id: 'dates', label: t('allocation.dates'), fixed: true, sort: r => r.fromDate, className: 'whitespace-nowrap tabular-nums', cell: r => <>{fmtDate(r.fromDate)} – {fmtDate(r.throughDate)}</> },
    { id: 'hours', label: t('allocation.plannedHours'), fixed: true, sort: r => r.plannedHours, className: 'text-right font-medium tabular-nums', cell: r => hours(r.plannedHours) },
    { id: 'status', label: t('common.status'), fixed: true, sort: r => r.status, cell: r => <RoleStatus status={r.status} /> },
    { id: 'filled', label: t('openRole.filledBy'), fixed: true, cell: r => r.filledAllocationId ? <FilledBy base={base} id={r.filledAllocationId} open={onOpenAllocation} /> : <span className="text-muted-foreground">{t('common.dash')}</span> },
  ]
  const table = useTable('hub.openRoleColumns', columns, roles.data ?? [], r => [r.fromDate, r.roleLabel])
  if (roles.isPending) return <div className="rounded-lg border bg-card"><Loading rows={3} /></div>
  if (roles.error) return <ErrorBanner error={roles.error} retry={() => roles.refetch()} />
  if (!roles.data.length) return <div className="rounded-lg border bg-card"><Empty title={t('openRole.empty')} action={proposal.shown && <Button variant="outline" {...proposal.props} onClick={onAdd}><Plus className="size-4" />{t('openRole.new')}</Button>}>{t('openRole.emptyHint')}</Empty></div>
  return <TableRegion><table className="w-full text-left text-sm">
    <caption className="sr-only">{t('openRole.tab')}</caption>
    <thead className="bg-muted text-muted-foreground"><tr>
      {columns.map(c => additions ? table.header(c) : <th key={c.id} scope="col" className={cn(th, c.className?.includes('text-right') && 'text-right')}>{c.label}</th>)}
      <th scope="col" className={th}><span className="sr-only">{t('common.actions')}</span></th>
    </tr></thead>
    <tbody>{(additions ? table.sorted : roles.data).map(r => <tr key={r.id} className={cn('border-t hover:bg-muted', r.status === 'Cancelled' && 'text-muted-foreground', current(r) && SELECTED_ROW)}>
      {columns.map(c => <td key={c.id} className={cn(td, c.className)}>{c.cell(r)}</td>)}
      <td className={cn(td, 'py-1 text-right')}>{r.status === 'Open' && <div className="flex items-center justify-end gap-1">
        {canFill && <Button size="sm" variant="outline" {...proposal.props} onClick={() => onFill(r)} aria-label={t('openRole.fillLabel', { role: r.roleLabel })}>{t('openRole.fill')}</Button>}
        {r.canManage && <DropdownMenu>
          <DropdownMenuTrigger asChild><Button size="icon-sm" variant="ghost" aria-label={t('openRole.actions', { role: r.roleLabel })}><Ellipsis className="size-4" aria-hidden /></Button></DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuItem onSelect={() => onEdit(r)}>{t('common.edit')}</DropdownMenuItem>
            <DropdownMenuItem className="text-bad" onSelect={() => onCancel(r)}>{t('openRole.cancel')}</DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>}
      </div>}</td>
    </tr>)}</tbody>
  </table></TableRegion>
}

function RoleStatus({ status }: { status: string }) { return <Pill tone={ROLE_TONE[status] ?? 'idle'}>{runtimeText(`openRole.status.${status}`)}</Pill> }

/** The read-only open-role panel (§47.5 FR-REC-12, behind flag.record_engine_additions): the role's fields and its History.
 *  Editing, filling and cancelling stay row actions with their dialogs. */
export function OpenRolePanel({ id }: PanelProps) {
  // ponytail: no open-role detail endpoint; the role comes from its project's list (the Allocations page's cached query), so the
  // panel opens under /projects/<number>/ only. Add a detail read if links from other pages need it.
  const number = useMatch('/projects/:number/*')?.params.number
  const project = useProject(number)
  const projectId = project.data?.id
  const roles = useQuery({ queryKey: ['allocations', projectId, 'open-roles'], enabled: !!projectId,
    queryFn: () => typedRequest('get', '/api/v1/projects/{projectId}/open-roles', { params: { projectId: projectId! } }) })
  const error = project.error ?? roles.error
  if (error) return <div className="p-4"><ErrorBanner error={error} retry={() => { void project.refetch(); void roles.refetch() }} /></div>
  if (number && (project.isPending || roles.isPending)) return <Loading />
  const r = roles.data?.find(x => x.id === id)
  if (!r) return <div className="p-4"><Notice title={t('app.notFound')} /></div>
  const value = (v: ReactNode) => <div className="break-words px-2 py-1.5">{v ?? <Missing />}</div>
  return <div>
    <PanelHead title={r.roleLabel} meta={<RoleStatus status={r.status} />} />
    <FieldGroup title={t('common.details')}>
      <FieldRow label={t('common.discipline')}>{value(r.disciplineName)}</FieldRow>
      <FieldRow label={t('allocation.from')}>{value(<span className="tabular-nums">{fmtDate(r.fromDate)}</span>)}</FieldRow>
      <FieldRow label={t('allocation.through')}>{value(<span className="tabular-nums">{fmtDate(r.throughDate)}</span>)}</FieldRow>
      <FieldRow label={t('allocation.plannedHours')}>{value(<span className="tabular-nums">{hours(r.plannedHours)}</span>)}</FieldRow>
      <FieldRow label={t('common.notes')}>{value(r.note)}</FieldRow>
    </FieldGroup>
    <TabBar tabs={[{ id: 'history', label: t('common.history') }]} value="history" onChange={() => {}} />
    <HistoryList type="OpenRole" id={id} />
  </div>
}

/** Who fills a role: the filling allocation's person, opening that allocation. Shows a dash when the viewer cannot open it. */
function FilledBy({ base, id, open }: { base: string; id: string; open: (id: string) => void }) {
  const q = useQuery({ queryKey: ['allocation-detail', base, id], queryFn: () => get<Detail>(`${base}/${id}`), retry: false })
  if (q.isPending) return <Spinner />
  if (!q.data) return <span className="text-muted-foreground">{t('common.dash')}</span>
  return <button type="button" className="flex items-center gap-2 text-left text-primary underline-offset-4 hover:underline" onClick={() => open(id)}>
    <Avatar id={q.data.personId} name={q.data.personName} /><span className="break-words">{q.data.personName ?? t('allocation.personUnavailable')}</span>
    <span className="sr-only">{tv(q.data.status)}</span>
  </button>
}

/** Add or edit an open role (AC-ROLE-01): label, optional discipline, dates, planned hours and a note. On a Complete project
 *  the change is a correction and needs a reason (A6). */
function RoleDialog({ projectId, disciplines, role, complete, onClose, onDone }: { projectId: string; disciplines: { id: string; name: string; isActive: boolean }[]; role?: OpenRole; complete: boolean; onClose: () => void; onDone: () => void }) {
  const [f, setF] = useState({ roleLabel: role?.roleLabel ?? '', projectDisciplineId: role?.projectDisciplineId ?? '', fromDate: role?.fromDate ?? today(), throughDate: role?.throughDate ?? today(),
    plannedHours: role ? String(role.plannedHours) : '', note: role?.note ?? '', reason: '' })
  const [err, setErr] = useState<ApiError | null>(null), [busy, setBusy] = useState(false)
  const safe = useSafeForm(role ? null : `role.new.${projectId}`, f, setF) // FR-SAV-03
  const labelMax = useReference().data?.limits.roleLabel // FR-FIG-12: the server's one limit
  const up = (k: keyof typeof f) => (e: { target: { value: string } }) => setF(x => ({ ...x, [k]: e.target.value }))
  const fe = err?.fieldErrors ?? {}
  const submit = async (e: React.FormEvent) => {
    e.preventDefault(); setErr(null); setBusy(true)
    const body = { roleLabel: f.roleLabel, projectDisciplineId: f.projectDisciplineId || null, fromDate: f.fromDate, throughDate: f.throughDate, plannedHours: Number(f.plannedHours), note: f.note || null, ...(complete ? { reason: f.reason } : {}) }
    try { await (role ? typedRequest('patch', '/api/v1/open-roles/{id}', { params: { id: role.id }, body, ifMatch: role.rowVersion }) : typedRequest('post', '/api/v1/projects/{projectId}/open-roles', { params: { projectId }, body })); safe.clear(); toast.success(t('common.saved')); onDone() }
    catch (x) { setErr(x as ApiError) } finally { setBusy(false) }
  }
  return <Dialog open onOpenChange={o => !o && !busy && safe.close(onClose)}><DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-lg">
    <DialogHeader><DialogTitle>{t(role ? 'openRole.edit' : 'openRole.new')}</DialogTitle><DialogDescription>{t('openRole.formHint')}</DialogDescription></DialogHeader>
    <DraftNotice form={safe} />
    <form className="grid gap-4 sm:grid-cols-2" onSubmit={submit}>
      <Field label={t('openRole.label')} htmlFor="role-label" error={fe.roleLabel} className="sm:col-span-2"><Input id="role-label" required maxLength={labelMax} autoFocus value={f.roleLabel} onChange={up('roleLabel')} placeholder={t('openRole.labelPlaceholder')} /></Field>
      <Field label={t('common.discipline')} htmlFor="role-discipline" error={fe.projectDisciplineId} optional className="sm:col-span-2">
        <select id="role-discipline" className={selectCls} value={f.projectDisciplineId} onChange={up('projectDisciplineId')}>
          <option value="">{t('common.none')}</option>{disciplines.filter(d => d.isActive || d.id === f.projectDisciplineId).map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
        </select></Field>
      <Field label={t('allocation.from')} htmlFor="role-from" error={fe.fromDate}><Input id="role-from" type="date" required value={f.fromDate} onChange={up('fromDate')} /></Field>
      <Field label={t('allocation.through')} htmlFor="role-through" error={fe.throughDate}><Input id="role-through" type="date" required min={f.fromDate} value={f.throughDate} onChange={up('throughDate')} /></Field>
      <Field label={t('allocation.plannedHours')} htmlFor="role-hours" error={fe.plannedHours} hint={t('openRole.hoursHint')}><Input id="role-hours" type="number" required min="0.01" max="10000" step="0.01" value={f.plannedHours} onChange={up('plannedHours')} /></Field>
      <Field label={t('common.notes')} htmlFor="role-note" error={fe.note} optional className="sm:col-span-2"><Textarea id="role-note" rows={2} maxLength={2000} value={f.note} onChange={up('note')} /></Field>
      {complete && <Field label={t('common.reason')} htmlFor="role-reason" error={fe.reason} hint={t('openRole.correctionHint')} className="sm:col-span-2"><Textarea id="role-reason" required minLength={5} value={f.reason} onChange={up('reason')} /></Field>}
      {err && <div className="sm:col-span-2"><ErrorBanner error={err} /></div>}
      <DialogFooter className="sm:col-span-2"><Button type="button" variant="outline" disabled={busy} onClick={() => safe.close(onClose)}>{t('common.cancel')}</Button>
        <Button type="submit" disabled={busy}>{busy && <Spinner />}{busy ? t('common.saving') : t('common.save')}</Button></DialogFooter>
    </form>
  </DialogContent></Dialog>
}
