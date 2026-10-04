import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, ChevronDown, ChevronRight, Link2, Plus, Send, X } from 'lucide-react'
import { Fragment, useMemo, useState, type ReactNode } from 'react'
import { Link, useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { ActiveFilters, ConfirmDialog, Empty, ErrorBanner, Field, FilterBar, Loading, Missing, Page, Spinner, TableRegion, selectCls } from '@/components/hub/common'
import { FieldRow, HistoryList, InlineDate, InlinePerson, InlineSelect, InlineText, TabBar } from '@/components/hub/fields'
import { DeliverableIndicators, progressLabel, type DeliverableStateView } from '@/components/hub/indicators'
import { PANELS, useItemPanel, type PanelProps } from '@/components/hub/panel-host'
import { PeoplePicker } from '@/components/hub/people'
import { Chip, Key, PriorityBadge, ProgressBar, StatusPill } from '@/components/hub/pills'
import { Why } from '@/components/hub/why'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { useProject, useProjectRefresh, useReference } from '@/hooks/data'
import { ApiError, del, get, patch, post, qs } from '@/lib/api'
import { fmtDate, today } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import type { ProjectDetail } from '@/lib/types'
import { cn } from '@/lib/utils'
import { DateText, FieldGroup, GroupRow, HeadCell, PanelHead, Person, RegisterCards, SELECTED_ROW, TITLE_LINK } from './Decisions'
import type { MilestoneRow } from './Milestones'
import { CommentsSlot, ItemSlots, LinksSlot, RaiseSlot } from './slots-items'
import { useCurrentProject } from './ProjectLayout'
import { errorText } from './Tasks'
import { ExportMenu } from '@/components/hub/export'
import { useTable } from '@/components/hub/table'
import { ViewMenu } from '@/components/hub/views'

export interface DeliverableRow {
  requiredReviewPackageId?: string
  id: string; projectId: string; key: string; name: string; projectDisciplineId: string; deliverableTypeId: string; ownerId?: string; reviewerId?: string
  milestoneId?: string; startDate?: string; dueDate?: string; originalDueDate?: string; originalStartDate?: string; priority: string; status: string; revision?: string; issuedDate?: string
  issuedTo?: string; requiresReview: boolean; rowVersion: number; createdAt: string; disciplineName?: string; disciplineColour?: string; disciplineOrder: number
  typeName?: string; ownerName?: string; ownerActive: boolean; reviewerName?: string; milestoneKey?: string; milestoneName?: string; milestoneDate?: string
  state?: (DeliverableStateView & { derivedPredecessorIds: string[]; derivedSuccessorIds: string[] }) | null
  tasks?: { id: string; key: string; name: string; status: string; dueDate?: string; assignee?: string }[]
}

const STATUSES = ['Not Started', 'In Progress', 'In Review', 'Revision Required', 'Ready to Issue', 'Issued', 'Accepted', 'On Hold', 'Cancelled']

/** Deliverables Register (§13.6): grouped by discipline by default, filters in the URL, expandable to tasks. */
export function DeliverablesTab() {
  const p = useCurrentProject()
  const ref = useReference()
  const [sp, setSp] = useSearchParams()
  const openPanel = useItemPanel()
  const [creating, setCreating] = useState(false)
  const [open, setOpen] = useState<Set<string>>(new Set())
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [bulk, setBulk] = useState<string | null>(null)
  const set = (k: string, v?: string | null) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); setSp(n, { replace: true }) }
  const filters = { disciplineId: sp.get('disciplineId'), status: sp.get('status'), milestoneId: sp.get('milestoneId'), ownerId: sp.get('ownerId'), typeId: sp.get('typeId'),
    indicator: sp.get('indicator'), dueFrom: sp.get('dueFrom'), dueTo: sp.get('dueTo'), q: sp.get('q'), requiresReview: sp.get('requiresReview') }
  const q = useQuery({ queryKey: ['p', p.id, 'deliverables', filters], queryFn: () => get<DeliverableRow[]>(`projects/${p.id}/deliverables${qs({ ...filters, includeTasks: true })}`) })
  const milestones = useQuery({ queryKey: ['p', p.id, 'milestones', false, ''], queryFn: () => get<MilestoneRow[]>(`projects/${p.id}/milestones`) })
  const group = sp.get('group') ?? 'discipline'
  const panel = sp.get('panel')
  const table = useTable<DeliverableRow>('hub.deliverableColumns', [
    { id: 'key', label: t('milestone.key'), fixed: true, sort: (d) => d.key, className: 'whitespace-nowrap', cell: (d) => <Key>{d.key}</Key> },
    { id: 'name', label: t('deliverable.name'), fixed: true, sort: (d) => d.name.toLowerCase(), className: 'min-w-[14rem]',
      cell: (d) => <button className={TITLE_LINK} aria-current={panel === `Deliverable:${d.id}` || undefined} onClick={() => openPanel('Deliverable', d.id)}>{d.name}</button> },
    { id: 'type', label: t('common.type'), sort: (d) => d.typeName, cell: (d) => d.typeName ?? <Missing /> },
    { id: 'discipline', label: t('common.discipline'), sort: (d) => d.disciplineOrder, className: 'whitespace-nowrap',
      cell: (d) => d.disciplineName ? <span className="inline-flex items-center gap-2"><span className="inline-block size-2.5 shrink-0 rounded-full" style={{ background: d.disciplineColour }} aria-hidden />{d.disciplineName}</span> : <Missing /> },
    { id: 'owner', label: t('common.owner'), sort: (d) => d.ownerName, className: 'whitespace-nowrap',
      cell: (d) => <Person id={d.ownerId} name={d.ownerName && (d.ownerActive ? d.ownerName : t('common.inactiveSuffix', { name: d.ownerName }))} /> },
    { id: 'reviewer', label: t('field.ReviewerId'), sort: (d) => d.reviewerName, className: 'whitespace-nowrap', cell: (d) => <Person id={d.reviewerId} name={d.reviewerName} /> },
    { id: 'milestone', label: t('field.MilestoneId'), sort: (d) => d.milestoneDate, className: 'whitespace-nowrap', cell: (d) => d.milestoneKey ? <span title={d.milestoneName}><Key>{d.milestoneKey}</Key></span> : <Missing /> },
    { id: 'due', label: t('common.due'), sort: (d) => d.dueDate, className: 'text-right', cell: (d) => <DateText date={d.dueDate} /> },
    { id: 'status', label: t('common.status'), sort: (d) => d.status, cell: (d) => <StatusPill status={d.status} /> },
    { id: 'progress', label: t('deliverable.progress'), sort: (d) => d.state?.progressPct, className: 'whitespace-nowrap text-right',
      cell: (d) => <span className="inline-flex items-center gap-2"><ProgressBar pct={d.state?.progressPct} label={progressLabel(d.state)} /><span className="text-xs/[18px] text-muted-foreground tabular-nums">{progressLabel(d.state)}</span></span> },
    { id: 'indicators', label: t('deliverable.indicators'), className: 'min-w-32', cell: (d) => <DeliverableIndicators s={d.state} /> },
    { id: 'revision', label: t('field.Revision'), sort: (d) => d.revision, cell: (d) => d.revision || <Missing /> },
    { id: 'issued', label: t('field.IssuedDate'), sort: (d) => d.issuedDate, className: 'text-right', cell: (d) => <DateText date={d.issuedDate} /> },
  ], q.data ?? [], (d) => [d.dueDate, d.key])
  const rows = table.sorted
  const groups = useMemo(() => {
    const key = (d: DeliverableRow) => group === 'milestone' ? (d.milestoneKey ? `${d.milestoneKey} ${d.milestoneName}` : t('deliverable.noMilestone'))
      : group === 'status' ? tv(d.status) : group === 'owner' ? d.ownerName ?? t('ind.unassigned') : group === 'none' ? '' : d.disciplineName ?? ''
    const map = new Map<string, DeliverableRow[]>()
    for (const d of [...rows].sort((a, b) => group === 'discipline' ? a.disciplineOrder - b.disciplineOrder : 0)) { const k = key(d); map.set(k, [...(map.get(k) ?? []), d]) }
    return [...map.entries()]
  }, [rows, group])
  const canCreate = p.permissions.createDeliverableIn.length > 0
  const toggle = (s: Set<string>, id: string) => { const n = new Set(s); if (n.has(id)) n.delete(id); else n.add(id); return n }
  const active = Object.values(filters).some(Boolean)
  const clear = () => { const n = new URLSearchParams(sp); for (const k of Object.keys(filters)) n.delete(k); setSp(n, { replace: true }) }
  const milestone = milestones.data?.find((m) => m.id === filters.milestoneId)
  // Active filters as removable tokens (§13.0 Filters), including owner and review filters that arrive by link.
  const tokens = [
    filters.q && { key: 'q', label: t('common.search'), value: filters.q },
    filters.disciplineId && { key: 'disciplineId', label: t('common.discipline'), value: p.disciplines.find((d) => d.id === filters.disciplineId)?.name ?? <Missing /> },
    filters.status && { key: 'status', label: t('common.status'), value: tv(filters.status) },
    filters.milestoneId && { key: 'milestoneId', label: t('field.MilestoneId'), value: milestone ? `${milestone.key} ${milestone.name}` : <Missing /> },
    filters.ownerId && { key: 'ownerId', label: t('common.owner'), value: q.data?.find((d) => d.ownerId === filters.ownerId)?.ownerName ?? <Missing /> },
    filters.typeId && { key: 'typeId', label: t('common.type'), value: ref.data?.deliverableTypes.find((x) => x.id === filters.typeId)?.name ?? <Missing /> },
    filters.indicator && { key: 'indicator', label: t('deliverable.indicator'), value: t(`dfilter.${filters.indicator}`) },
    filters.dueFrom && { key: 'dueFrom', label: t('tfilter.dueFrom'), value: fmtDate(filters.dueFrom) },
    filters.dueTo && { key: 'dueTo', label: t('tfilter.dueTo'), value: fmtDate(filters.dueTo) },
    filters.requiresReview && { key: 'requiresReview', label: t('field.RequiresReview'), value: filters.requiresReview === 'true' ? t('common.yes') : t('common.no') },
  ].filter(Boolean) as { key: string; label: string; value: ReactNode }[]

  return (
    <Page title={t('ptab.deliverables')} subtitle={t('deliverable.subtitle')}
      actions={<>
        {table.menu}
        <ViewMenu listType="deliverables" projectId={p.id} extra={() => ({ cols: table.colsParam })} />
        <ExportMenu path={`projects/${p.id}/deliverables/export`} params={filters} name={`${p.projectNumber}-deliverables`} />
        {canCreate && <Button onClick={() => setCreating(true)}><Plus className="size-4" />{t('deliverable.new')}</Button>}
      </>}>
      <FilterBar>
        <div className="flex flex-wrap items-end gap-3">
          <Field label={t('common.search')} htmlFor="deliverable-search" className="w-full sm:w-56">
            <Input id="deliverable-search" type="search" value={filters.q ?? ''} onChange={(e) => set('q', e.target.value)} />
          </Field>
          <Field label={t('common.discipline')} htmlFor="deliverable-discipline" className="w-full sm:w-44">
            <select id="deliverable-discipline" className={selectCls} value={filters.disciplineId ?? ''} onChange={(e) => set('disciplineId', e.target.value)}>
              <option value="">{t('projects.anyDiscipline')}</option>{p.disciplines.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
          </Field>
          <Field label={t('common.status')} htmlFor="deliverable-status" className="w-full sm:w-44">
            <select id="deliverable-status" className={selectCls} value={filters.status ?? ''} onChange={(e) => set('status', e.target.value)}>
              <option value="">{t('deliverable.anyStatus')}</option>{STATUSES.map((s) => <option key={s} value={s}>{tv(s)}</option>)}
            </select>
          </Field>
          <Field label={t('field.MilestoneId')} htmlFor="deliverable-milestone" className="w-full sm:w-52">
            <select id="deliverable-milestone" className={selectCls} value={filters.milestoneId ?? ''} onChange={(e) => set('milestoneId', e.target.value)}>
              <option value="">{t('deliverable.anyMilestone')}</option>{(milestones.data ?? []).map((m) => <option key={m.id} value={m.id}>{m.key} {m.name}</option>)}
            </select>
          </Field>
          <Field label={t('common.type')} htmlFor="deliverable-type" className="w-full sm:w-44">
            <select id="deliverable-type" className={selectCls} value={filters.typeId ?? ''} onChange={(e) => set('typeId', e.target.value)}>
              <option value="">{t('deliverable.anyType')}</option>{ref.data?.deliverableTypes.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
            </select>
          </Field>
          <Field label={t('deliverable.indicator')} htmlFor="deliverable-indicator" className="w-full sm:w-44">
            <select id="deliverable-indicator" className={selectCls} value={filters.indicator ?? ''} onChange={(e) => set('indicator', e.target.value)}>
              <option value="">{t('deliverable.anyIndicator')}</option>
              {['overdue', 'atRisk', 'dueSoon', 'unassigned', 'dateInconsistent', 'slipped', 'open'].map((x) => <option key={x} value={x}>{t(`dfilter.${x}`)}</option>)}
            </select>
          </Field>
          <Field label={t('tfilter.dueFrom')} htmlFor="deliverable-due-from" className="w-full sm:w-44">
            <Input id="deliverable-due-from" type="date" value={filters.dueFrom ?? ''} onChange={(e) => set('dueFrom', e.target.value)} />
          </Field>
          <Field label={t('tfilter.dueTo')} htmlFor="deliverable-due-to" className="w-full sm:w-44">
            <Input id="deliverable-due-to" type="date" value={filters.dueTo ?? ''} onChange={(e) => set('dueTo', e.target.value)} />
          </Field>
          <Field label={t('common.groupBy')} htmlFor="deliverable-group" className="w-full sm:w-48">
            <select id="deliverable-group" className={selectCls} value={group} onChange={(e) => set('group', e.target.value === 'discipline' ? null : e.target.value)}>
              {['discipline', 'milestone', 'status', 'owner', 'none'].map((g) => <option key={g} value={g}>{t(`dgroup.${g}`)}</option>)}
            </select>
          </Field>
        </div>
        <ActiveFilters tokens={tokens} onRemove={(k) => set(k, null)} onClear={clear} />
      </FilterBar>
      {selected.size > 0 && (
        <div className="flex flex-wrap items-center gap-2 rounded-lg border bg-accent px-4 py-2 text-sm" role="region" aria-label={t('bulk.actions')}>
          <span className="mr-1 font-semibold tabular-nums text-accent-foreground">{t('bulk.selected', { n: selected.size })}</span>
          <Button size="sm" variant="outline" onClick={() => setBulk('setMilestone')}>{t('bulk.setMilestone')}</Button>
          <Button size="sm" variant="outline" onClick={() => setBulk('shiftDueDates')}>{t('bulk.shiftDue')}</Button>
          <Button size="sm" variant="outline" onClick={() => setBulk('setOwner')}>{t('bulk.setOwner')}</Button>
          <Button size="sm" variant="ghost" onClick={() => setSelected(new Set())}>{t('common.clear')}</Button>
        </div>
      )}
      {q.isPending ? <div className="rounded-lg border bg-card"><Loading rows={8} /></div> : q.error ? <ErrorBanner error={q.error} retry={() => q.refetch()} /> : rows.length === 0 ? (
        <div className="rounded-lg border bg-card">{active
          ? <Empty title={t('register.noMatch')} action={<Button variant="outline" onClick={clear}>{t('filters.clear')}</Button>}>{t('register.noMatchHint')}</Empty>
          : <Empty action={canCreate && <Button variant="outline" onClick={() => setCreating(true)}><Plus className="size-4" />{t('deliverable.new')}</Button>}>{t('deliverable.empty')}</Empty>}</div>
      ) : (<>
        <RegisterCards groups={groups.map(([g, list]) => ({ label: group === 'none' ? '' : g, rows: list }))} columns={table.visible} current={(d) => panel === `Deliverable:${d.id}`} />
        <TableRegion className="hidden md:block">
          <table className="w-full text-sm">
            <caption className="sr-only">{t('ptab.deliverables')}</caption>
            <thead className="bg-muted text-left text-muted-foreground">
              <tr>
                <th scope="col" className="w-10 px-(--cell-px)"><Checkbox aria-label={t('bulk.selectAll')} checked={selected.size === rows.length} onCheckedChange={(c) => setSelected(c ? new Set(rows.map((r) => r.id)) : new Set())} /></th>
                <th scope="col" className="w-10"><span className="sr-only">{t('common.details')}</span></th>
                {table.visible.map((c) => <HeadCell key={c.id} th={table.header(c)} right={c.className?.includes('text-right')} />)}
              </tr>
            </thead>
            {groups.map(([g, list]) => (
              <tbody key={g}>
                {group !== 'none' && <GroupRow span={table.visible.length + 2} label={g} count={list.length} />}
                {list.map((d) => (
                  <Fragment key={d.id}>
                    <tr className={cn('border-t hover:bg-muted', selected.has(d.id) && 'bg-accent hover:bg-accent', panel === `Deliverable:${d.id}` && SELECTED_ROW)}>
                      <td className="px-(--cell-px)"><Checkbox aria-label={`${t('bulk.select')} ${d.key}`} checked={selected.has(d.id)} onCheckedChange={() => setSelected(toggle(selected, d.id))} /></td>
                      <td className="px-1">{(d.tasks?.length ?? 0) > 0 && <button className="grid size-(--control-row-h) place-items-center rounded-md hover:bg-secondary" aria-expanded={open.has(d.id)} aria-label={t('deliverable.showTasks')} onClick={() => setOpen(toggle(open, d.id))}>
                        {open.has(d.id) ? <ChevronDown className="size-4" /> : <ChevronRight className="size-4" />}</button>}</td>
                      {table.visible.map((c) => table.cell(c, d))}
                    </tr>
                    {open.has(d.id) && d.tasks?.map((tk) => (
                      <tr key={tk.id} className="border-t bg-muted">
                        <td /><td />
                        <td colSpan={table.visible.length} className="px-(--cell-px) py-2">
                          <span className="flex flex-wrap items-center gap-x-4 gap-y-1"><Key>{tk.key}</Key>
                            <button className="break-words text-left hover:underline" onClick={() => openPanel('Task', tk.id)}>{tk.name}</button>
                            <span className="text-muted-foreground">{tk.assignee ?? t('ind.unassigned')}</span><DateText date={tk.dueDate} /><StatusPill status={tk.status} /></span>
                        </td>
                      </tr>
                    ))}
                  </Fragment>
                ))}
              </tbody>
            ))}
          </table>
        </TableRegion>
      </>)}
      {creating && <CreateDeliverable p={p} milestones={milestones.data ?? []} onClose={() => setCreating(false)} />}
      {bulk && <BulkDialog p={p} op={bulk} ids={[...selected]} milestones={milestones.data ?? []} onClose={(ok) => { setBulk(null); if (ok) { setSelected(new Set()); q.refetch() } }} />}
    </Page>
  )
}

function BulkDialog({ p, op, ids, milestones, onClose }: { p: ProjectDetail; op: string; ids: string[]; milestones: MilestoneRow[]; onClose: (ok: boolean) => void }) {
  const [v, setV] = useState<string | null>('')
  return (
    <ConfirmDialog open onOpenChange={(o) => !o && onClose(false)} title={t(`bulk.${op === 'setMilestone' ? 'setMilestone' : op === 'setOwner' ? 'setOwner' : 'shiftDue'}`)} reason={op === 'shiftDueDates'}
      body={t('bulk.selected', { n: ids.length })} busy={!v}
      onConfirm={async (reason) => {
        const params = op === 'setMilestone' ? { milestoneId: v } : op === 'setOwner' ? { ownerId: v } : { days: Number(v) }
        const r = await post(`projects/${p.id}/deliverables/bulk`, { ids, operation: op, params, reason })
        toast.success(t('bulk.result', { updated: r.updated, skipped: r.skipped.length }))
        onClose(true)
      }}>
      {op === 'setMilestone' && <Field label={t('field.MilestoneId')} htmlFor="bulk-milestone"><select id="bulk-milestone" className={selectCls} value={v ?? ''} onChange={(e) => setV(e.target.value)}>
        <option value="">{t('common.selectPlaceholder')}</option>{milestones.map((m) => <option key={m.id} value={m.id}>{m.key} {m.name}</option>)}</select></Field>}
      {op === 'setOwner' && <Field label={t('common.owner')} htmlFor="bulk-owner"><PeoplePicker id="bulk-owner" value={v} onChange={setV} /></Field>}
      {op === 'shiftDueDates' && <Field label={t('bulk.days')} htmlFor="bulk-days"><Input id="bulk-days" type="number" value={v ?? ''} onChange={(e) => setV(e.target.value)} /></Field>}
    </ConfirmDialog>
  )
}

function CreateDeliverable({ p, milestones, onClose }: { p: ProjectDetail; milestones: MilestoneRow[]; onClose: () => void }) {
  const ref = useReference()
  const refresh = useProjectRefresh()
  const allowed = p.disciplines.filter((d) => d.isActive && p.permissions.createDeliverableIn.includes(d.id))
  const [f, setF] = useState<Record<string, any>>({ projectDisciplineId: allowed[0]?.id, requiresReview: true, priority: 'Medium' })
  const [owner, setOwner] = useState<{ id: string | null; name?: string }>({ id: null })
  const [reviewer, setReviewer] = useState<string | null>(null)
  const [err, setErr] = useState<ApiError | null>(null)
  const lead = p.disciplines.find((d) => d.id === f.projectDisciplineId)
  const submit = async () => {
    setErr(null)
    try {
      const r = await post(`projects/${p.id}/deliverables`, { ...f, ownerId: owner.id ?? lead?.leadUserId ?? null, reviewerId: reviewer, milestoneId: f.milestoneId || null,
        startDate: f.startDate || null, dueDate: f.dueDate || null })
      toast.success(t('deliverable.created', { key: r.key }))
      r.warnings?.forEach((w: string) => toast.warning(w))
      refresh(p.id); onClose()
    } catch (e) { setErr(e as ApiError) }
  }
  const fe = err?.fieldErrors ?? {}
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-w-xl">
        <DialogHeader><DialogTitle>{t('deliverable.new')}</DialogTitle></DialogHeader>
        <form className="grid gap-3 sm:grid-cols-2" onSubmit={(e) => { e.preventDefault(); submit() }}>
          <Field label={t('deliverable.name')} htmlFor="d-name" error={fe.name} className="sm:col-span-2"><Input id="d-name" required placeholder={t('deliverable.namePlaceholder')} value={f.name ?? ''} onChange={(e) => setF({ ...f, name: e.target.value })} /></Field>
          <Field label={t('common.discipline')} htmlFor="d-disc">
            <select id="d-disc" className={selectCls} value={f.projectDisciplineId ?? ''} onChange={(e) => setF({ ...f, projectDisciplineId: e.target.value })}>{allowed.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}</select>
          </Field>
          <Field label={t('common.type')} htmlFor="d-type" error={fe.deliverableTypeId}>
            <select id="d-type" className={selectCls} required value={f.deliverableTypeId ?? ''} onChange={(e) => setF({ ...f, deliverableTypeId: e.target.value })}>
              <option value="">{t('common.selectPlaceholder')}</option>{ref.data?.deliverableTypes.filter((x) => x.isActive).map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
            </select>
          </Field>
          <Field label={t('common.owner')} error={fe.ownerId} hint={!owner.id && lead?.leadName ? t('deliverable.ownerDefault', { name: lead.leadName }) : undefined}>
            <PeoplePicker value={owner.id} valueName={owner.name} onChange={(id, x) => setOwner({ id, name: x?.displayName })} />
          </Field>
          <Field label={t('field.ReviewerId')} error={fe.reviewerId}><PeoplePicker value={reviewer} onChange={setReviewer} /></Field>
          <Field label={t('field.MilestoneId')} htmlFor="d-ms" hint={t('deliverable.dueDefault')}>
            <select id="d-ms" className={selectCls} value={f.milestoneId ?? ''} onChange={(e) => setF({ ...f, milestoneId: e.target.value })}>
              <option value="">{t('common.none')}</option>{milestones.map((m) => <option key={m.id} value={m.id}>{m.key} {m.name} · {fmtDate(m.date)}</option>)}
            </select>
          </Field>
          <Field label={t('common.priority')} htmlFor="d-prio">
            <select id="d-prio" className={selectCls} value={f.priority} onChange={(e) => setF({ ...f, priority: e.target.value })}>{['Low', 'Medium', 'High', 'Critical'].map((x) => <option key={x} value={x}>{tv(x)}</option>)}</select>
          </Field>
          <Field label={t('field.StartDate')} htmlFor="d-start" error={fe.startDate}><Input id="d-start" type="date" value={f.startDate ?? ''} onChange={(e) => setF({ ...f, startDate: e.target.value })} /></Field>
          <Field label={t('field.DueDate')} htmlFor="d-due"><Input id="d-due" type="date" value={f.dueDate ?? ''} onChange={(e) => setF({ ...f, dueDate: e.target.value })} /></Field>
          <label className="flex items-center gap-2 text-sm sm:col-span-2"><Checkbox checked={f.requiresReview} onCheckedChange={(c) => setF({ ...f, requiresReview: !!c })} />{t('field.RequiresReview')}</label>
          <Field label={t('common.description')} htmlFor="d-desc" className="sm:col-span-2"><Textarea id="d-desc" rows={2} value={f.description ?? ''} onChange={(e) => setF({ ...f, description: e.target.value })} /></Field>
          {err && <div className="sm:col-span-2"><ErrorBanner error={err} /></div>}
          <DialogFooter className="sm:col-span-2"><Button type="button" variant="outline" onClick={onClose}>{t('common.cancel')}</Button><Button type="submit">{t('common.save')}</Button></DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

// ---------- Deliverable detail panel (§13.6.1) ----------

export const DELIVERABLE_TABS: { id: string; label: string; render: (d: any) => React.ReactNode }[] = []

function DeliverablePanel({ id }: PanelProps) {
  const q = useQuery({ queryKey: ['deliverable', id], queryFn: () => get(`deliverables/${id}`) })
  const qc = useQueryClient()
  const refresh = useProjectRefresh()
  const ref = useReference()
  const [tab, setTab] = useState('tasks')
  const [transition, setTransition] = useState<{ to: string; needsReason: boolean } | null>(null)
  const [issuing, setIssuing] = useState(false)
  const [err, setErr] = useState<unknown>(null)
  const project = useProject(q.data?.project.projectNumber)
  const milestones = useQuery({ queryKey: ['p', q.data?.project.id, 'milestones', false, ''], queryFn: () => get<MilestoneRow[]>(`projects/${q.data!.project.id}/milestones`), enabled: !!q.data })
  const openPanel = useItemPanel()
  if (q.isPending) return <Loading />
  if (q.error) return <div className="p-4"><ErrorBanner error={q.error} /></div>
  const data = q.data
  const d: DeliverableRow = data.deliverable
  const perms = data.permissions
  const can = perms.edit.ok
  const reload = () => { qc.invalidateQueries({ queryKey: ['deliverable', id] }); qc.invalidateQueries({ queryKey: ['p', d.projectId] }); refresh(d.projectId) }
  // The outcome lets each inline field say Saved or Not saved (design §8); the banner keeps the reason.
  const save = async (body: object): Promise<boolean> => {
    setErr(null)
    try {
      const r = await patch(`deliverables/${d.id}`, body, d.rowVersion)
      r.warnings?.forEach((w: string) => toast.warning(w)); reload()
      return true
    } catch (e) {
      if ((e as ApiError).code === 'confirm_move_tasks' && confirm((e as ApiError).message)) return save({ ...body, confirmMoveTasks: true })
      setErr(e)
      return false
    }
  }
  const s = d.state
  return (
    <div>
      <PanelHead title={d.name} meta={<>
        <Key>{d.key}</Key>
        <DropdownMenu>
          <DropdownMenuTrigger asChild><button className="inline-flex items-center gap-1 rounded-md focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring" aria-label={t('deliverable.changeStatus')}><StatusPill status={d.status} /><ChevronDown className="size-4 text-muted-foreground" aria-hidden /></button></DropdownMenuTrigger>
          <DropdownMenuContent align="start">
            {perms.transitions.map((x: any) => (
              <DropdownMenuItem key={x.to} disabled={!x.allowed} onSelect={() => setTransition({ to: x.to, needsReason: x.needsReason })}>{tv(x.to)}</DropdownMenuItem>
            ))}
            {perms.readyToIssueGuard && d.status !== 'In Review' && <DropdownMenuItem disabled title={perms.readyToIssueGuard}>{tv('Ready to Issue')} — {perms.readyToIssueGuard}</DropdownMenuItem>}
          </DropdownMenuContent>
        </DropdownMenu>
        <PriorityBadge priority={d.priority} />
      </>}>
        <DeliverableIndicators s={s} />
        {s?.isAtRisk && <Why reasons={s.atRiskReasons as any} title={t('ind.atRisk')}><span className="text-sm text-muted-foreground">{t('deliverable.whyAtRisk')}</span></Why>}
        <div className="flex flex-wrap gap-2">
          {can && <Button size="sm" variant="outline" asChild><Link to={`/projects/${data.project.projectNumber}/handoffs?source=${d.id}`}>{t('handoff.new')}</Link></Button>}
          <Button size="sm" variant="outline" asChild><Link to={`/projects/${data.project.projectNumber}/changes?target=Deliverable:${d.id}`}>{t('change.inputs')}</Link></Button>
          {d.requiredReviewPackageId && <Button size="sm" variant="outline" asChild><Link to={`/projects/${data.project.projectNumber}/reviews?panel=ReviewPackage:${d.requiredReviewPackageId}`}>{t('review.issueGate')}</Link></Button>}
          <RaiseSlot projectId={d.projectId} targetType="Deliverable" targetId={d.id} targetKey={d.key} targetName={d.name} disciplineId={d.projectDisciplineId} />
          {perms.issue && <Button size="sm" onClick={() => setIssuing(true)}><Send className="size-4" />{t('deliverable.issue')}</Button>}
        </div>
      </PanelHead>
      {err != null && <div className="px-4 pb-4"><ErrorBanner error={err} retry={() => { setErr(null); reload() }} /></div>}
      <FieldGroup title={t('common.details')}>
        <FieldRow label={t('common.name')}><InlineText value={d.name} disabled={!can} onSave={(v) => save({ name: v })} /></FieldRow>
        <FieldRow label={t('common.type')}><InlineSelect value={d.deliverableTypeId} disabled={!can} options={(ref.data?.deliverableTypes ?? []).filter((x) => x.isActive || x.id === d.deliverableTypeId).map((x) => ({ value: x.id, label: x.name }))} onSave={(v) => save({ deliverableTypeId: v })} title={t('common.type')} /></FieldRow>
        <FieldRow label={t('common.discipline')}><InlineSelect value={d.projectDisciplineId} disabled={!can} options={(project.data?.disciplines ?? []).filter((x) => x.isActive || x.id === d.projectDisciplineId).map((x) => ({ value: x.id, label: x.name }))} onSave={(v) => save({ projectDisciplineId: v })} title={t('common.discipline')} /></FieldRow>
        <FieldRow label={t('common.priority')}><InlineSelect value={d.priority} disabled={!can} options={['Low', 'Medium', 'High', 'Critical'].map((x) => ({ value: x, label: tv(x) }))} onSave={(v) => save({ priority: v })} title={t('common.priority')} /></FieldRow>
        <FieldRow label={t('common.description')}><InlineText value={data.description} multiline disabled={!can} onSave={(v) => save({ description: v })} /></FieldRow>
      </FieldGroup>
      <FieldGroup title={t('register.group.ownership')}>
        <FieldRow label={t('common.owner')}><InlinePerson value={d.ownerId} name={d.ownerName} disabled={!can} allowClear={false} onSave={(v) => save({ ownerId: v })} /></FieldRow>
        <FieldRow label={t('field.ReviewerId')}><InlinePerson value={d.reviewerId} name={d.reviewerName} disabled={!can} onSave={(v) => save({ reviewerId: v })} /></FieldRow>
      </FieldGroup>
      <FieldGroup title={t('register.group.schedule')}>
        <FieldRow label={t('field.MilestoneId')}><InlineSelect value={d.milestoneId} allowEmpty disabled={!can} options={(milestones.data ?? []).map((m) => ({ value: m.id, label: `${m.key} ${m.name}` }))} onSave={(v) => save({ milestoneId: v })} title={t('field.MilestoneId')} /></FieldRow>
        <FieldRow label={t('field.StartDate')}><InlineDate value={d.startDate} disabled={!can} onSave={(v) => save({ startDate: v })} /></FieldRow>
        <FieldRow label={t('field.DueDate')}><InlineDate value={d.dueDate} disabled={!can} onSave={(v) => save({ dueDate: v })} />{d.originalDueDate && d.originalDueDate !== d.dueDate && <span className="px-2 text-xs/[18px] text-muted-foreground tabular-nums">{t('deliverable.originally', { date: fmtDate(d.originalDueDate) })}</span>}</FieldRow>
        <FieldRow label={t('deliverable.progress')}><div className="px-2 py-1.5"><span className="inline-flex items-center gap-2"><ProgressBar pct={s?.progressPct} /><span className="text-xs/[18px] text-muted-foreground tabular-nums">{progressLabel(s) ?? t('deliverable.noTasks')}</span></span>
          {s && <div className="mt-1 text-xs/[18px] text-muted-foreground tabular-nums">{t('deliverable.breakdown', { open: s.taskOpen, overdue: s.taskOverdue, blocked: s.taskBlocked })}</div>}</div></FieldRow>
      </FieldGroup>
      <FieldGroup title={t('register.group.reviewIssue')}>
        <FieldRow label={t('field.RequiresReview')}><div className="px-2 py-1.5"><Checkbox checked={d.requiresReview} disabled={!can} onCheckedChange={(c) => save({ requiresReview: !!c })} aria-label={t('field.RequiresReview')} /></div></FieldRow>
        <FieldRow label={t('field.Revision')}><InlineText value={d.revision} disabled={!can} onSave={(v) => save({ revision: v })} /></FieldRow>
        {d.issuedDate && <FieldRow label={t('deliverable.issued')}><div className="px-2 py-1.5"><span className="tabular-nums">{fmtDate(d.issuedDate)}</span> · {d.revision} · {d.issuedTo}{data.transmittalUrl && <> · <a className="text-primary underline underline-offset-4" href={data.transmittalUrl} target="_blank" rel="noreferrer noopener">{t('deliverable.transmittal')}</a></>}</div></FieldRow>}
        {data.onHoldReason && d.status === 'On Hold' && <FieldRow label={t('field.OnHoldReason')}><div className="px-2 py-1.5">{data.onHoldReason}</div></FieldRow>}
      </FieldGroup>
      <TabBar tabs={[{ id: 'tasks', label: t('ptab.tasks'), count: d.tasks?.length }, { id: 'deps', label: t('deliverable.dependencies') }, { id: 'issues', label: t('deliverable.issues'), count: data.issues.length },
        ...(ItemSlots.Links ? [{ id: 'links', label: t('common.links') }] : []), ...(ItemSlots.Comments ? [{ id: 'comments', label: t('common.comments') }] : []),
        ...DELIVERABLE_TABS.map((x) => ({ id: x.id, label: x.label })), { id: 'history', label: t('common.history') }]} value={tab} onChange={setTab} />
      {tab === 'tasks' && (
        <div className="p-4">
          {(d.tasks ?? []).length === 0 ? <Empty>{t('deliverable.noTasks')}</Empty> : (
            <ul className="divide-y rounded-md border">
              {d.tasks!.map((tk) => (
                <li key={tk.id} className="flex min-h-(--row-min) flex-wrap items-center gap-x-3 gap-y-1 px-3 py-(--cell-py) text-sm">
                  <Key>{tk.key}</Key><button className="min-w-0 flex-1 truncate text-left hover:underline" onClick={() => openPanel('Task', tk.id)}>{tk.name}</button>
                  <span className="text-muted-foreground">{tk.assignee ?? t('ind.unassigned')}</span><DateText date={tk.dueDate} /><StatusPill status={tk.status} />
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
      {tab === 'deps' && (
        <div className="grid gap-4 p-4 text-sm">
          <ExplicitDeps d={d} data={data} onChange={reload} />
          <p className="text-xs/[18px] text-muted-foreground">{t('deliverable.derivedHint')}</p>
          {[['deliverable.dependsOn', data.derivedPredecessors], ['deliverable.blocks', data.derivedSuccessors]].map(([label, list]: any) => (
            <div key={label}><div className="mb-1 font-medium">{t(label)}</div>
              {list.length === 0 ? <p className="text-muted-foreground">{t('common.none')}</p> : <ul className="space-y-1">{list.map((x: any) => <li key={x.id} className="flex flex-wrap items-center gap-2"><Key>{x.key}</Key><button className="text-left hover:underline" onClick={() => openPanel('Deliverable', x.id)}>{x.name}</button><StatusPill status={x.status} /></li>)}</ul>}
            </div>
          ))}
        </div>
      )}
      {tab === 'issues' && <IssueHistory issues={data.issues} />}
      {tab === 'links' && <LinksSlot type="Deliverable" id={d.id} projectId={d.projectId} />}
      {tab === 'comments' && <CommentsSlot type="Deliverable" id={d.id} projectId={d.projectId} />}
      {DELIVERABLE_TABS.filter((x) => x.id === tab).map((x) => <div key={x.id}>{x.render(data)}</div>)}
      {tab === 'history' && <HistoryList type="Deliverable" id={d.id} />}
      {transition && <DeliverableTransition d={d} to={transition.to} needsReason={transition.needsReason} onClose={() => { setTransition(null); reload() }} />}
      {issuing && <IssueDialog d={d} needsConfirm={perms.issueNeedsConfirm} onClose={() => { setIssuing(false); reload() }} />}
    </div>
  )
}

function DeliverableTransition({ d, to, needsReason, onClose }: { d: DeliverableRow; to: string; needsReason: boolean; onClose: () => void }) {
  const isRevision = to === 'Revision Required'
  const [comment, setComment] = useState('')
  return (
    <ConfirmDialog open onOpenChange={(o) => !o && onClose()} title={t('deliverable.transitionTitle', { key: d.key, to: tv(to) })}
      reason={needsReason && !isRevision ? true : undefined} busy={isRevision && comment.trim().length === 0}
      onConfirm={async (reason) => { await post(`deliverables/${d.id}/transition`, { toStatus: to, reason, comment: comment || undefined, rowVersion: d.rowVersion }) }}>
      <Field label={isRevision ? t('deliverable.revisionComment') : t('deliverable.statusNote')} htmlFor="dt-comment" hint={isRevision ? t('deliverable.revisionHint') : t('common.optional')}>
        <Textarea id="dt-comment" rows={3} value={comment} onChange={(e) => setComment(e.target.value)} />
      </Field>
    </ConfirmDialog>
  )
}

interface DepRow { dependencyId: string; lagDays: number; note?: string; id: string; key: string; name: string; status: string; dueDate?: string; issuedDate?: string }

/** Explicit Finish-to-Start links to other deliverables (FR-DEP-09, packet 021), with an optional lag, apart from derived ones. */
function ExplicitDeps({ d, data, onChange }: { d: DeliverableRow; data: any; onChange: () => void }) {
  const openPanel = useItemPanel()
  const [adding, setAdding] = useState<'predecessor' | 'successor' | null>(null)
  const can: boolean = data.permissions.linkDeliverables
  const satisfied = (x: DepRow) => ['Issued', 'Accepted', 'Cancelled'].includes(x.status)
  const remove = async (x: DepRow) => { try { await del(`deliverable-dependencies/${x.dependencyId}`); onChange() } catch (e) { toast.error(errorText(e)) } }
  const list = (rows: DepRow[], label: string, dir: 'predecessor' | 'successor') => (
    <div>
      <div className="mb-1 flex items-center justify-between font-medium">{t(label)}
        {can && <Button size="sm" variant="ghost" onClick={() => setAdding(dir)}><Plus className="size-4" />{t('common.add')}</Button>}</div>
      {rows.length === 0 ? <p className="text-muted-foreground">{t('common.none')}</p> : (
        <ul className="divide-y rounded-md border bg-card">{rows.map((x) => (
          <li key={x.dependencyId} className="flex flex-wrap items-center gap-2 px-3 py-1.5">
            {satisfied(x) ? <Check className="size-4 text-done" aria-label={t('task.satisfied')} /> : <Link2 className="size-4 text-muted-foreground" aria-hidden />}
            <button className="min-w-0 flex-1 truncate text-left hover:underline" onClick={() => openPanel('Deliverable', x.id)}><Key>{x.key}</Key> {x.name}</button>
            {x.lagDays > 0 && <Chip tone="idle">{t('dep.lagN', { n: x.lagDays })}</Chip>}
            {x.note && <span className="hidden max-w-40 truncate text-xs/[18px] text-muted-foreground sm:inline" title={x.note}>{x.note}</span>}
            <DateText date={x.issuedDate ?? x.dueDate} /><StatusPill status={x.status} />
            {can && <Button variant="ghost" size="icon-sm" aria-label={t('task.removeDependency', { key: x.key })} onClick={() => remove(x)}><X className="size-4" /></Button>}
          </li>))}</ul>
      )}
    </div>
  )
  return (
    <div className="grid gap-3 rounded-lg border bg-muted p-4">
      <div className="text-sm font-semibold">{t('deliverable.explicitDeps')}</div>
      {list(data.explicitPredecessors, 'deliverable.dependsOn', 'predecessor')}
      {list(data.explicitSuccessors, 'deliverable.blocks', 'successor')}
      {adding && <LinkDeliverable d={d} direction={adding} exclude={[d.id, ...data.explicitPredecessors.map((x: DepRow) => x.id), ...data.explicitSuccessors.map((x: DepRow) => x.id)]}
        onClose={(ok) => { setAdding(null); if (ok) onChange() }} />}
    </div>
  )
}

function LinkDeliverable({ d, direction, exclude, onClose }: { d: DeliverableRow; direction: 'predecessor' | 'successor'; exclude: string[]; onClose: (ok: boolean) => void }) {
  const list = useQuery({ queryKey: ['p', d.projectId, 'deliverables', {}], queryFn: () => get<DeliverableRow[]>(`projects/${d.projectId}/deliverables`) })
  const [choice, setChoice] = useState('')
  const [lag, setLag] = useState('0')
  const [note, setNote] = useState('')
  const [err, setErr] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const save = async () => {
    setErr(null); setBusy(true)
    try {
      await post(`deliverables/${d.id}/dependencies`, { [direction === 'predecessor' ? 'predecessorDeliverableId' : 'successorDeliverableId']: choice, lagDays: Number(lag) || 0, note: note || null })
      onClose(true)
    } catch (e) {
      const x = e as ApiError
      setErr(x.code === 'dependency_cycle' ? t('task.cycle', { path: (x.body.cyclePath ?? []).join(' → ') }) : errorText(e))
    } finally { setBusy(false) }
  }
  return (
    <Dialog open onOpenChange={(o) => !o && onClose(false)}>
      <DialogContent>
        <DialogHeader><DialogTitle>{t(direction === 'predecessor' ? 'deliverable.linkNeeds' : 'deliverable.linkHoldsUp', { key: d.key })}</DialogTitle><DialogDescription>{t('deliverable.linkHint')}</DialogDescription></DialogHeader>
        <div className="grid gap-3">
          <Field label={t('deliverable.linkOther')} htmlFor="ld-other">
            <select id="ld-other" className={selectCls} value={choice} onChange={(e) => setChoice(e.target.value)}>
              <option value="">{t('deliverable.chooseDeliverable')}</option>
              {(list.data ?? []).filter((x) => !exclude.includes(x.id) && x.status !== 'Cancelled').map((x) => <option key={x.id} value={x.id}>{x.key} · {x.name}</option>)}
            </select>
          </Field>
          <Field label={t('dep.lag')} htmlFor="ld-lag" hint={t('dep.lagHint')}><Input id="ld-lag" type="number" min={0} max={365} className="w-28" value={lag} onChange={(e) => setLag(e.target.value)} /></Field>
          <Field label={t('common.notes')} htmlFor="ld-note" hint={t('common.optional')}><Input id="ld-note" value={note} onChange={(e) => setNote(e.target.value)} placeholder={t('dep.notePlaceholder')} /></Field>
          {err && <div role="alert" className="rounded-md border border-bad/30 bg-bad-bg px-4 py-3 text-sm text-bad">{err}</div>}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onClose(false)}>{t('common.cancel')}</Button>
          <Button disabled={!choice || busy} onClick={save}>{busy && <Spinner />}{busy ? t('common.saving') : t('common.add')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

interface IssueRow { id: string; issuedDate: string; revision?: string; issuedTo?: string; transmittalUrl?: string; note?: string; issuedBy?: string }

/** Every issue of the deliverable, newest first, so Rev A stays on record after Rev B goes out (FR-DEL-08, E-24). */
function IssueHistory({ issues }: { issues: IssueRow[] }) {
  if (!issues.length) return <div className="p-4"><Empty>{t('deliverable.noIssues')}</Empty></div>
  return (
    <div className="scroll-region overflow-x-auto p-4">
      <table className="w-full text-sm">
        <thead className="text-left text-muted-foreground">
          <tr>{['field.IssuedDate', 'field.Revision', 'field.IssuedTo', 'deliverable.transmittalLink', 'common.notes', 'deliverable.issuedBy'].map((h) => <th key={h} scope="col" className="whitespace-nowrap py-2 pr-3 font-medium">{t(h)}</th>)}</tr>
        </thead>
        <tbody>{issues.map((x) => (
          <tr key={x.id} className="border-t align-top">
            <td className="py-2 pr-3"><DateText date={x.issuedDate} /></td>
            <td className="whitespace-nowrap py-2 pr-3 font-medium">{x.revision ?? <Missing />}</td>
            <td className="py-2 pr-3">{x.issuedTo}</td>
            <td className="py-2 pr-3">{x.transmittalUrl && <a className="text-primary underline underline-offset-4" href={x.transmittalUrl} title={x.transmittalUrl} target="_blank" rel="noreferrer noopener">{t('deliverable.transmittal')}</a>}</td>
            <td className="whitespace-pre-wrap py-2 pr-3">{x.note}</td>
            <td className="whitespace-nowrap py-2">{x.issuedBy}</td>
          </tr>))}
        </tbody>
      </table>
    </div>
  )
}

/** "Issue deliverable" (§12.4 UI, DL-05, DL-06): date, revision, recipient, transmittal link, note. */
function IssueDialog({ d, needsConfirm, onClose }: { d: DeliverableRow; needsConfirm: boolean; onClose: () => void }) {
  const [f, setF] = useState<Record<string, string>>({ issuedDate: today(), revision: d.revision ?? '' })
  const [open, setOpen] = useState<any[] | null>(null)
  return (
    <ConfirmDialog open onOpenChange={(o) => !o && onClose()} title={t('deliverable.issueTitle', { key: d.key, name: d.name })} confirmLabel={t('deliverable.issue')}
      body={open ? t('deliverable.openTasksConfirm', { n: open.length }) : needsConfirm ? t('deliverable.issueOutOfSequence', { status: tv(d.status) }) : undefined}
      onConfirm={async () => {
        try { await post(`deliverables/${d.id}/issue`, { ...f, confirmOpenTasks: !!open, rowVersion: d.rowVersion }); toast.success(t('deliverable.issuedToast', { key: d.key })) }
        catch (e) { if ((e as ApiError).code === 'confirm_open_tasks') { setOpen((e as ApiError).body.tasks); throw new ApiError(409, { detail: t('deliverable.confirmAgain') }) } throw e }
      }}>
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label={t('field.IssuedDate')} htmlFor="i-date"><Input id="i-date" type="date" max={today()} value={f.issuedDate} onChange={(e) => setF({ ...f, issuedDate: e.target.value })} /></Field>
        <Field label={t('field.Revision')} htmlFor="i-rev"><Input id="i-rev" placeholder="Rev A" value={f.revision} onChange={(e) => setF({ ...f, revision: e.target.value })} /></Field>
        <Field label={t('field.IssuedTo')} htmlFor="i-to" className="sm:col-span-2"><Input id="i-to" placeholder={t('deliverable.issuedToPlaceholder')} value={f.issuedTo ?? ''} onChange={(e) => setF({ ...f, issuedTo: e.target.value })} /></Field>
        <Field label={t('deliverable.transmittalLink')} htmlFor="i-link" className="sm:col-span-2"><Input id="i-link" placeholder="https://…" value={f.transmittalUrl ?? ''} onChange={(e) => setF({ ...f, transmittalUrl: e.target.value })} /></Field>
        <Field label={t('common.notes')} htmlFor="i-note" className="sm:col-span-2"><Textarea id="i-note" rows={2} value={f.note ?? ''} onChange={(e) => setF({ ...f, note: e.target.value })} /></Field>
      </div>
      {open && <ul className="max-h-32 space-y-1 overflow-y-auto rounded-md border p-3 text-sm">{open.map((x) => <li key={x.id}><span className="key">{x.key}</span> {x.name} · {tv(x.status)}</li>)}</ul>}
    </ConfirmDialog>
  )
}

PANELS.Deliverable = { component: DeliverablePanel }
