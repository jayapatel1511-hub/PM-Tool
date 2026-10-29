import { PickerProject } from '@/components/hub/people'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ChevronDown, ChevronRight, Lock, Monitor, MoreHorizontal, Plus } from 'lucide-react'
import { Fragment, useState } from 'react'
import { Link } from 'react-router'
import { toast } from 'sonner'
import { ConfirmDialog, DesktopOnly, Empty, ErrorBanner, Field, FilterBar, Loading, Notice, Page, Spinner, selectCls, tdCls, thCls, useIsPhone } from '@/components/hub/common'
import { FieldRow, HistoryList, InlineText, TabBar } from '@/components/hub/fields'
import { useItemPanel, type PanelProps } from '@/components/hub/panel-host'
import { Chip, Key, ProgressBar, StatusPill, toneOf } from '@/components/hub/pills'
import { DataTable } from '@/components/hub/registers'
import { useTable, type Column } from '@/components/hub/table'
import { Why } from '@/components/hub/why'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { useProject, useProjectRefresh, useReference } from '@/hooks/data'
import { useFlag } from '@/lib/auth'
import { ApiError, get, patch, post, qs, typedRequest } from '@/lib/api'
import { daysBetween, fmtDate, relative, shortDate, today } from '@/lib/format'
import { t, tv , runtimeText } from '@/lib/i18n'
import type { ProjectDetail } from '@/lib/types'
import { cn } from '@/lib/utils'
import { CommentsSlot, ItemSlots } from './slots-items'
import { STATUSES as DELIVERABLE_STATUSES } from './Deliverables'
import { useCurrentProject } from './ProjectLayout'
import { ExportMenu } from '@/components/hub/export'
import { DeletedList, DeletedToggle, useShowDeleted } from '@/components/hub/deleted'
import { DraftNotice } from '@/components/hub/drafts'
import { useSafeForm } from '@/hooks/drafts'

export interface MilestoneRow {
  id: string; projectId: string; key: string; name: string; milestoneType: string; date?: string; originalDate?: string; description?: string
  projectDisciplineId?: string; completesPhaseId?: string; isClientFacing: boolean; isComplete: boolean; completedDate?: string; isCancelled: boolean
  cancelledReason?: string; rowVersion: number; isSubmission: boolean; status?: string | null; statusReasons?: any[] | null; slipDays: number; baselineDate?: string; sinceBaselineDays?: number; canRebaseline?: boolean; baselineSlipShown?: boolean; slipShown?: boolean
  daysRemaining?: number | null; deliverableTotal: number; deliverableIssued: number; taskTotal: number; taskComplete: number; taskOpen: number
  taskOverdue: number; taskBlocked: number
}

const TYPES = ['Kickoff', 'Field Work', 'Design Submission', 'Client Workshop', 'Permit Submission', 'Tender', 'Construction', 'IFC', 'Record Drawings', 'Closeout', 'Other'] as const

export function milestoneStatusLabel(m: MilestoneRow) {
  if (!m.date && !m.isComplete && !m.isCancelled) return t('milestone.undated')
  return m.status ?? t('milestone.notEvaluated')
}

/** The lists behind a milestone's counts (FR-RCV-09): each opens rows whose number equals the figure shown. Totals leave out
 *  Cancelled, as the milestone's evaluated counts do, so the deliverable total carries every other status. */
function milestoneLinks(base: string, m: Pick<MilestoneRow, 'id'>) {
  const del = (status: string[]) => `${base}/deliverables?${new URLSearchParams({ milestoneId: m.id, status: status.join(',') })}`
  const tasks = (extra = '') => `${base}/tasks?milestoneId=${m.id}${extra}`
  return {
    deliverables: del(DELIVERABLE_STATUSES.filter((s) => s !== 'Cancelled')), issued: del(['Issued', 'Accepted']),
    tasks: tasks(), complete: tasks('&status=Complete'), overdue: tasks('&overdue=true'), blocked: tasks('&blocked=true'),
  }
}

const countLink = 'rounded-sm underline-offset-2 hover:underline'
/** "2/3 issued" and "1/3" tasks with each number opening its list; data-count names the figure for the counts suite. */
function DeliverableCount({ m, base }: { m: MilestoneRow; base: string }) {
  const l = milestoneLinks(base, m)
  return <span className="tabular-nums">
    <Link className={countLink} to={l.issued} data-count="Issued" aria-label={t('milestone.link.issued', { n: m.deliverableIssued, key: m.key })}>{m.deliverableIssued}</Link>/<Link
      className={countLink} to={l.deliverables} data-count="Deliverables" aria-label={t('milestone.link.deliverables', { n: m.deliverableTotal, key: m.key })}>{m.deliverableTotal}</Link> {t('milestone.issuedWord')}
  </span>
}
function TaskCount({ m, base }: { m: MilestoneRow; base: string }) {
  const l = milestoneLinks(base, m)
  return <span className="tabular-nums">
    <Link className={countLink} to={l.complete} data-count="Complete" aria-label={t('milestone.link.complete', { n: m.taskComplete, key: m.key })}>{m.taskComplete}</Link>/<Link
      className={countLink} to={l.tasks} data-count="Tasks" aria-label={t('milestone.link.tasks', { n: m.taskTotal, key: m.key })}>{m.taskTotal}</Link>
  </span>
}
function TaskChips({ m, base }: { m: MilestoneRow; base: string }) {
  const l = milestoneLinks(base, m)
  return <>
    {m.taskOverdue > 0 && <Link to={l.overdue} data-count="Overdue" className="rounded-full"><Chip tone="bad">{t('ind.overdueN', { n: m.taskOverdue })}</Chip></Link>}
    {m.taskBlocked > 0 && <Link to={l.blocked} data-count="Blocked" className="rounded-full"><Chip tone="bad">{t('ind.blockedN', { n: m.taskBlocked })}</Chip></Link>}
  </>
}

/** "+5 d", "−3 d", "0 d": slip from the original date, with a true minus sign. */
const slip = (n: number) => t('milestone.slipDays', { n: n > 0 ? `+${n}` : n < 0 ? `−${-n}` : '0' })

/** Milestone editing is desktop/tablet only (§13.0): phones read milestones and get this explanation instead. */
const PhoneNotice = ({ className }: { className?: string }) => <Notice icon={Monitor} title={t('milestone.phoneTitle')} className={className}>{t('milestone.phoneHint')}</Notice>

/** Milestone View (§13.7): strip with slip ghosts, table with readiness, and the PM's lifecycle actions. */
function LiveMilestonesTab() {
  const p = useCurrentProject()
  const phone = useIsPhone()
  const ref = useReference()
  const [showCompleted, setShowCompleted] = useState(false)
  const [type, setType] = useState('')
  const [open, setOpen] = useState<Set<string>>(new Set())
  const [dialog, setDialog] = useState<{ kind: 'new' | 'edit' | 'date' | 'complete' | 'cancel' | 'reopen'; m?: MilestoneRow } | null>(null)
  const openPanel = useItemPanel()
  const q = useQuery({ queryKey: ['p', p.id, 'milestones', showCompleted, type], queryFn: () => get<MilestoneRow[]>(`projects/${p.id}/milestones${qs({ showCompleted, type })}`) })
  const can = p.permissions.manageMilestones.ok
  const rows = q.data ?? []
  const disc = (id?: string) => p.disciplines.find((d) => d.id === id)?.name
  const base = `/projects/${p.projectNumber}`
  const toggle = (id: string) => { const n = new Set(open); if (n.has(id)) n.delete(id); else n.add(id); setOpen(n) }
  // Every column is fixed and none sorts: the list keeps its columns, order and header text (FR-REC-14 adds no surface).
  const table = useTable<MilestoneRow>('hub.milestoneColumns', ([
    { id: 'details', label: t('common.details'), srOnly: true, className: 'w-12 px-2',
      cell: (m) => <button type="button" aria-label={t('milestone.showDeliverables', { key: m.key })} aria-expanded={open.has(m.id)} className="grid size-(--control-row-h) place-items-center rounded-md hover:bg-secondary"
        onClick={() => toggle(m.id)}>{open.has(m.id) ? <ChevronDown className="size-4" /> : <ChevronRight className="size-4" />}</button> },
    { id: 'key', label: t('milestone.key'), className: 'whitespace-nowrap', cell: (m) => <Key>{m.key}</Key> },
    { id: 'name', label: t('common.name'), className: 'min-w-48',
      cell: (m) => <button type="button" className="break-words text-left font-semibold hover:underline" onClick={() => openPanel('Milestone', m.id)}>{m.isSubmission && '◆ '}{m.name}</button> },
    { id: 'type', label: t('common.type'), cell: (m) => runtimeText(`mtype.${m.milestoneType}`) },
    { id: 'date', label: t('common.date'), className: 'whitespace-nowrap tabular-nums', cell: (m) => fmtDate(m.date) },
    { id: 'original', label: t('milestone.original'), className: 'whitespace-nowrap tabular-nums text-muted-foreground', cell: (m) => fmtDate(m.originalDate) },
    { id: 'slip', label: t('milestone.slip'), className: 'whitespace-nowrap text-right tabular-nums',
      cell: (m) => <span className={cn(m.slipDays > 0 && 'font-semibold text-warn')}>{m.slipDays > 0 && <span aria-hidden className="mr-1 text-xs">▲</span>}{slip(m.slipDays)}</span> },
    { id: 'status', label: t('common.status'), cell: (m) => <Why reasons={m.statusReasons} title={t('milestone.statusWhy', { key: m.key })}><StatusPill status={milestoneStatusLabel(m)} /></Why> },
    { id: 'remaining', label: t('milestone.remaining'), className: 'whitespace-nowrap tabular-nums', cell: (m) => m.isComplete ? fmtDate(m.completedDate) : m.date ? relative(m.date) : t('common.dash') },
    { id: 'deliverables', label: t('milestone.deliverables'), className: 'whitespace-nowrap', cell: (m) => <DeliverableCount m={m} base={base} /> },
    { id: 'tasks', label: t('milestone.tasks'), cell: (m) => <>
      <TaskCount m={m} base={base} />
      {(m.taskOverdue > 0 || m.taskBlocked > 0) && <span className="mt-1 flex flex-wrap gap-1"><TaskChips m={m} base={base} /></span>}
    </> },
    { id: 'discipline', label: t('common.discipline'), cell: (m) => disc(m.projectDisciplineId) ?? t('common.dash') },
    { id: 'clientFacing', label: t('milestone.clientFacing'), cell: (m) => m.isClientFacing ? t('common.yes') : t('common.no') },
    { id: 'actions', label: t('common.actions'), srOnly: true, className: 'w-12 px-2 text-right',
      cell: (m) => can && <DesktopOnly notice={null}><MilestoneMenu m={m} onPick={(kind) => setDialog({ kind, m })} /></DesktopOnly> },
  ] as Column<MilestoneRow>[]).map((c) => ({ ...c, fixed: true, className: cn('align-top', c.className) })), rows, (m) => [m.date, m.key])
  const create = (variant?: 'outline') => <DesktopOnly notice={null}><Button variant={variant} onClick={() => setDialog({ kind: 'new' })}><Plus className="size-4" />{t('milestone.new')}</Button></DesktopOnly>
  return (
    <Page title={t('ptab.milestones')} actions={<>
      <ExportMenu path={`projects/${p.id}/milestones/export`} params={{ showCompleted, type }} name={`${p.projectNumber}-milestones`} />
      {can && create()}
    </>}>
      {!can && p.permissions.manageMilestones.reason && <Notice icon={Lock} title={p.permissions.manageMilestones.reason} />}
      {can && <DesktopOnly notice={<PhoneNotice />}>{null}</DesktopOnly>}
      <MilestoneStrip rows={rows} onOpen={(m) => openPanel('Milestone', m.id)} />
      <FilterBar activeCount={(type ? 1 : 0) + (showCompleted ? 1 : 0)}>
        <DeletedToggle project={p} />
        <div className="flex flex-wrap items-end gap-3">
          <Field label={t('common.type')} htmlFor="ms-type" className="w-full sm:w-56">
            <select id="ms-type" className={selectCls} value={type} onChange={(e) => setType(e.target.value)}>
              <option value="">{t('milestone.anyType')}</option>{TYPES.map((x) => <option key={x} value={x}>{t(`mtype.${x}`)}</option>)}
            </select>
          </Field>
          <label className="flex min-h-(--control-h) items-center gap-2 text-sm"><Checkbox checked={showCompleted} onCheckedChange={(c) => setShowCompleted(!!c)} />{t('milestone.showCompleted')}</label>
        </div>
      </FilterBar>
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      {phone && !q.isPending && rows.length > 0 ? (
        <ul className="space-y-3">{rows.map((m) => <li key={m.id} className="rounded-lg border bg-card p-4">
          <div className="mb-2 flex flex-wrap items-center gap-2"><Key>{m.key}</Key><StatusPill status={milestoneStatusLabel(m)} /></div>
          <button type="button" className="mb-3 break-words text-left text-base/6 font-semibold text-primary hover:underline" onClick={() => openPanel('Milestone', m.id)}>{m.isSubmission && '◆ '}{m.name}</button>
          <dl className="grid grid-cols-[minmax(5rem,auto)_minmax(0,1fr)] gap-x-3 gap-y-2 text-sm">
            {[
              [t('common.type'), runtimeText(`mtype.${m.milestoneType}`)],
              [t('common.date'), fmtDate(m.date)], [t('milestone.original'), fmtDate(m.originalDate)],
              [t('milestone.slip'), slip(m.slipDays)],
              [t('milestone.remaining'), m.isComplete ? fmtDate(m.completedDate) : m.date ? relative(m.date) : t('common.dash')],
              [t('milestone.deliverables'), <DeliverableCount key="d" m={m} base={base} />],
              [t('milestone.tasks'), <TaskCount key="t" m={m} base={base} />],
              [t('common.discipline'), disc(m.projectDisciplineId) ?? t('common.dash')],
              [t('milestone.clientFacing'), m.isClientFacing ? t('common.yes') : t('common.no')],
            ].map(([label, value]) => <Fragment key={label as string}><dt className="text-muted-foreground">{label}</dt><dd className="min-w-0 break-words tabular-nums">{value}</dd></Fragment>)}
          </dl>
          <div className="mt-3 flex flex-wrap gap-2">
            <TaskChips m={m} base={base} />
            <Why reasons={m.statusReasons} title={t('milestone.statusWhy', { key: m.key })}><span className="text-sm text-primary underline">{t('common.why')}</span></Why>
          </div>
          <Button className="mt-3 h-auto min-h-(--control-h) max-w-full whitespace-normal text-left" variant="outline" aria-expanded={open.has(m.id)} onClick={() => toggle(m.id)}>{t('milestone.showDeliverables', { key: m.key })}</Button>
          {open.has(m.id) && <div className="mt-3 border-t pt-3"><TargetedDeliverables milestoneId={m.id} /></div>}
        </li>)}</ul>
      ) : (
        <DataTable table={table} rows={rows} loading={q.isPending} caption={t('ptab.milestones')} current={() => false} cards={false}
          detail={(m) => open.has(m.id) ? <TargetedDeliverables milestoneId={m.id} /> : null}
          empty={<Empty action={can && create('outline')}>{t('milestone.empty')}</Empty>} />
      )}
      {dialog && <MilestoneDialogs p={p} kind={dialog.kind} m={dialog.m} phases={ref.data?.phases ?? []} onClose={() => setDialog(null)} />}
    </Page>
  )
}

export function MilestonesTab() {
  const p = useCurrentProject()
  if (useShowDeleted(p)) return <Page title={t('ptab.milestones')}><FilterBar><DeletedToggle project={p} /></FilterBar><DeletedList project={p} kind="milestones" /></Page>
  return <LiveMilestonesTab />
}

function MilestoneMenu({ m, onPick }: { m: MilestoneRow; onPick: (k: 'edit' | 'date' | 'complete' | 'cancel' | 'reopen') => void }) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild><Button variant="ghost" size="icon-sm" aria-label={t('milestone.actionsFor', { key: m.key })}><MoreHorizontal className="size-4" /></Button></DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuItem onSelect={() => onPick('edit')}>{t('common.edit')}</DropdownMenuItem>
        {!m.isComplete && !m.isCancelled && m.date && <DropdownMenuItem onSelect={() => onPick('date')}>{t('milestone.changeDate')}</DropdownMenuItem>}
        {!m.isComplete && !m.isCancelled && <DropdownMenuItem onSelect={() => onPick('complete')}>{t('milestone.complete')}</DropdownMenuItem>}
        {m.isComplete && <DropdownMenuItem onSelect={() => onPick('reopen')}>{t('milestone.reopen')}</DropdownMenuItem>}
        {!m.isComplete && !m.isCancelled && <DropdownMenuItem variant="destructive" onSelect={() => onPick('cancel')}>{t('milestone.cancel')}</DropdownMenuItem>}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

function TargetedDeliverables({ milestoneId }: { milestoneId: string }) {
  const phone = useIsPhone()
  const q = useQuery({ queryKey: ['milestone', milestoneId], queryFn: () => get(`milestones/${milestoneId}`) })
  const openPanel = useItemPanel()
  if (q.isPending) return <Loading rows={2} />
  const list: any[] = q.data?.deliverables ?? []
  if (!list.length) return <p className="py-1 text-sm text-muted-foreground">{t('milestone.noDeliverables')}</p>
  if (phone) return <ul className="divide-y rounded-md border bg-card">{list.map(d => <li key={d.id} className="space-y-2 p-3 text-sm"><Key>{d.key}</Key><button type="button" className="block break-words text-left font-semibold text-primary hover:underline" onClick={() => openPanel('Deliverable', d.id)}>{d.name}</button><StatusPill status={d.status} /><p>{t('common.owner')}: {d.ownerName || t('common.dash')}</p><p>{t('common.due')}: {fmtDate(d.dueDate)}</p><ProgressBar pct={d.state?.progressPct} />{d.state && <p className="text-xs/[18px] text-muted-foreground">{t('milestone.taskCounts', { open: d.state.taskOpen, overdue: d.state.taskOverdue, blocked: d.state.taskBlocked })}</p>}</li>)}</ul>
  return (
    <div className="scroll-region overflow-x-auto rounded-md border bg-card">
      <table className="w-full text-sm">
        <caption className="sr-only">{t('milestone.deliverables')}</caption>
        <thead className="bg-muted">
          <tr>{(['milestone.key', 'common.name', 'common.status', 'common.owner', 'common.due', 'projects.col.progress', 'milestone.tasks'] as const).map((h) => <th key={h} scope="col" className={thCls}>{t(h)}</th>)}</tr>
        </thead>
        <tbody>
          {list.map((d) => (
            <tr key={d.id} className="border-t">
              <td className={cn(tdCls, 'whitespace-nowrap')}><Key>{d.key}</Key></td>
              <td className={tdCls}><button type="button" className="break-words text-left font-medium hover:underline" onClick={() => openPanel('Deliverable', d.id)}>{d.name}</button></td>
              <td className={tdCls}><StatusPill status={d.status} /></td>
              <td className={tdCls}>{d.ownerName}</td>
              <td className={cn(tdCls, 'whitespace-nowrap tabular-nums')}>{fmtDate(d.dueDate)}</td>
              <td className={tdCls}><ProgressBar pct={d.state?.progressPct} /></td>
              <td className={cn(tdCls, 'whitespace-nowrap text-xs/[18px] text-muted-foreground tabular-nums')}>{d.state ? t('milestone.taskCounts', { open: d.state.taskOpen, overdue: d.state.taskOverdue, blocked: d.state.taskBlocked }) : ''}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

const SYMBOL = { ok: '●', warn: '▲', bad: '■', idle: '○', done: '✓', work: '◐' } as const
const AXIS = 30, LANE = 40

/** Strip of milestones on a date axis with a today marker (§13.7): labelled diamonds coloured by status with the status
 *  symbol beside the date, a hollow ghost at the original date and the slip when a milestone has moved. */
export function MilestoneStrip({ rows, onOpen, limit }: { rows: MilestoneRow[]; onOpen: (m: MilestoneRow) => void; limit?: number }) {
  const dated = rows.filter((m) => m.date && !m.isCancelled).slice(0, limit ?? 100)
  if (!dated.length) return null
  const now = today()
  const dates = [...dated.flatMap((m) => [m.date!, m.originalDate ?? m.date!, m.baselineDate ?? m.date!]), now]
  const min = dates.reduce((a, b) => (a < b ? a : b)), max = dates.reduce((a, b) => (a > b ? a : b))
  const span = Math.max(daysBetween(min, max), 1)
  const x = (d: string) => 4 + (daysBetween(min, d) / span) * 92
  const tone = (m: MilestoneRow) => toneOf(m.isComplete ? 'Complete' : m.status)
  // Each label is centred under its diamond (anchored inwards at the edges) and takes the first of three lanes it clears.
  // Preserve the readable date-axis width. Short keys identify diamonds; full names stay in the adjacent table/cards.
  const lanes: number[] = []
  const placed = [...dated].sort((a, b) => a.date!.localeCompare(b.date!)).map((m) => {
    const left = x(m.date!)
    const anchor = left < 12 ? 'start' : left > 88 ? 'end' : 'middle'
    const from = anchor === 'start' ? left - 1 : anchor === 'end' ? left - 12 : left - 6
    let lane = lanes.findIndex((end) => end <= from)
    if (lane < 0) lane = lanes.length < 3 ? lanes.length : lanes.indexOf(Math.min(...lanes))
    lanes[lane] = from + 12
    return { m, left, lane, anchor }
  })
  const todayX = x(now)
  return (
    <div className="scroll-region overflow-x-auto rounded-lg border bg-card" role="group" aria-label={t('milestone.stripLabel', { n: dated.length })}>
      <div className="min-w-[900px] px-5 py-4"><div className="relative" style={{ height: AXIS + 16 + lanes.length * LANE }}>
        <div aria-hidden className="absolute inset-x-0 h-px bg-border" style={{ top: AXIS }} />
        <div className="absolute border-l-2 border-dashed border-primary" style={{ left: `${todayX}%`, top: 0, height: AXIS + 12 }}>
          <span className={cn('absolute top-0 whitespace-nowrap text-xs/[18px] font-semibold text-primary', todayX > 85 ? 'right-1.5' : 'left-1.5')}>{t('common.today')}</span>
        </div>
        {/* Each milestone is a list item (display: contents keeps the absolute layout), so the strip reads as a list of milestones. */}
        <div role="list" className="contents">{placed.map(({ m, left, lane, anchor }) => {
          const status = milestoneStatusLabel(m)
          const color = `var(--${tone(m)})`
          const label = anchor === 'start' ? { left: `calc(${left}% - 8px)` } : anchor === 'end' ? { right: `calc(${100 - left}% - 8px)`, textAlign: 'right' as const }
            : { left: `${left}%`, transform: 'translateX(-50%)', textAlign: 'center' as const }
          return (
            <div role="listitem" key={m.id} className="contents">
              {m.slipShown !== false && m.originalDate && m.originalDate !== m.date && (
                <span aria-hidden className="absolute size-3 rotate-45 border-2 bg-card" style={{ left: `calc(${x(m.originalDate)}% - 6px)`, top: AXIS - 6, borderColor: color }}
                  title={`${m.name}: ${t('milestone.original')} ${fmtDate(m.originalDate)}`} />
              )}
              {lane > 0 && <span aria-hidden className="absolute w-px bg-border" style={{ left: `${left}%`, top: AXIS + 12, height: lane * LANE }} />}
              {m.baselineDate && m.baselineSlipShown !== false && m.baselineDate !== m.date && <span aria-hidden className="absolute size-3 rotate-45 border-2 border-dotted bg-card" style={{ left: `calc(${x(m.baselineDate)}% - 6px)`, top: AXIS - 6, borderColor: color }} title={t('interaction.baselineDate', { date: fmtDate(m.baselineDate) })} />}
              <button type="button" className="absolute z-10 grid size-6 -translate-x-1/2 place-items-center rounded-md hover:bg-muted" style={{ left: `${left}%`, top: AXIS - 12 }} onClick={() => onOpen(m)}
                title={`${m.key} ${m.name} · ${fmtDate(m.date)} · ${tv(status)}`} aria-label={`${m.key} ${m.name}, ${fmtDate(m.date)}, ${tv(status)}`}>
                <span className="block size-3.5 rotate-45 border border-white shadow-[0_0_0_1px_rgba(25,27,32,0.3)]" style={{ background: color }} />
              </button>
              <span aria-hidden className="pointer-events-none absolute w-max max-w-32 text-xs/[18px]" style={{ ...label, top: AXIS + 14 + lane * LANE }}>
                <span className="block font-medium">{m.key}</span>
                <span className="block whitespace-nowrap text-muted-foreground tabular-nums">
                  {SYMBOL[tone(m)]} {shortDate(m.date)}{m.slipShown !== false && m.slipDays > 0 && <span className="text-warn"> · {slip(m.slipDays)}</span>}
                </span>
              </span>
            </div>
          )
        })}</div>
      </div></div>
    </div>
  )
}

function MilestoneDialogs({ p, kind, m, phases, onClose }: { p: ProjectDetail; kind: string; m?: MilestoneRow; phases: { id: string; name: string; isActive: boolean }[]; onClose: () => void }) {
  const refresh = useProjectRefresh()
  const qc = useQueryClient()
  const done = () => { qc.invalidateQueries({ queryKey: ['p', p.id] }); qc.invalidateQueries({ queryKey: ['milestone'] }); refresh(p.id); onClose() }
  if (kind === 'new' || kind === 'edit') return <MilestoneForm p={p} m={m} phases={phases} onClose={onClose} onDone={done} />
  if (kind === 'date') return <ChangeDateDialog m={m!} onClose={onClose} onDone={done} />
  if (kind === 'complete') return <CompleteDialog p={p} m={m!} onClose={onClose} onDone={done} />
  if (kind === 'reopen') return <ConfirmDialog open onOpenChange={(o) => !o && onClose()} title={t('milestone.reopenTitle', { key: m!.key })} reason
    onConfirm={async (reason) => { await post(`milestones/${m!.id}/reopen`, { reason, rowVersion: m!.rowVersion }); done() }} />
  return <CancelDialog p={p} m={m!} onClose={onClose} onDone={done} />
}

function MilestoneForm({ p, m, phases, onClose, onDone }: { p: ProjectDetail; m?: MilestoneRow; phases: { id: string; name: string; isActive: boolean }[]; onClose: () => void; onDone: () => void }) {
  const [f, setF] = useState<Record<string, any>>(m ? { name: m.name, milestoneType: m.milestoneType, description: m.description ?? '', projectDisciplineId: m.projectDisciplineId ?? '',
    completesPhaseId: m.completesPhaseId ?? '', isClientFacing: m.isClientFacing, date: m.date ?? '' } : { milestoneType: 'Design Submission', isClientFacing: true })
  const [err, setErr] = useState<ApiError | null>(null)
  const [busy, setBusy] = useState(false)
  const safe = useSafeForm(m ? null : `milestone.new.${p.id}`, f, setF) // FR-SAV-03: a new milestone keeps a draft
  const submit = async () => {
    setErr(null); setBusy(true)
    try {
      const body: Record<string, any> = { ...f, projectDisciplineId: f.projectDisciplineId || null, completesPhaseId: f.completesPhaseId || null }
      if (m) { if (m.date) delete body.date; await patch(`milestones/${m.id}`, body, m.rowVersion) } else await post(`projects/${p.id}/milestones`, body)
      safe.clear(); toast.success(t('common.saved')); onDone()
    } catch (e) { setErr(e as ApiError) } finally { setBusy(false) }
  }
  return (
    <Dialog open onOpenChange={(o) => !o && safe.close(onClose)}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-xl">
        <DialogHeader><DialogTitle>{m ? t('milestone.edit', { key: m.key }) : t('milestone.new')}</DialogTitle></DialogHeader>
        <DraftNotice form={safe} />
        <form className="grid gap-4 sm:grid-cols-2" onSubmit={(e) => { e.preventDefault(); submit() }}>
          <Field label={t('common.name')} htmlFor="m-name" error={err?.fieldErrors.name} className="sm:col-span-2"><Input id="m-name" required value={f.name ?? ''} onChange={(e) => setF({ ...f, name: e.target.value })} /></Field>
          <Field label={t('common.type')} htmlFor="m-type">
            <select id="m-type" className={selectCls} value={f.milestoneType} onChange={(e) => setF({ ...f, milestoneType: e.target.value })}>{TYPES.map((x) => <option key={x} value={x}>{t(`mtype.${x}`)}</option>)}</select>
          </Field>
          {(!m || !m.date) && <Field label={t('common.date')} htmlFor="m-date" error={err?.fieldErrors.date}><Input id="m-date" type="date" required value={f.date ?? ''} onChange={(e) => setF({ ...f, date: e.target.value })} /></Field>}
          <Field label={t('milestone.disciplineTag')} htmlFor="m-disc" optional>
            <select id="m-disc" className={selectCls} value={f.projectDisciplineId} onChange={(e) => setF({ ...f, projectDisciplineId: e.target.value })}>
              <option value="">{t('common.none')}</option>{p.disciplines.filter((d) => d.isActive).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
          </Field>
          <Field label={t('field.CompletesPhaseId')} htmlFor="m-phase" optional>
            <select id="m-phase" className={selectCls} value={f.completesPhaseId} onChange={(e) => setF({ ...f, completesPhaseId: e.target.value })}>
              <option value="">{t('common.none')}</option>{phases.filter((x) => x.isActive).map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
            </select>
          </Field>
          <label className="flex min-h-6 items-center gap-2 text-sm sm:col-span-2"><Checkbox checked={!!f.isClientFacing} onCheckedChange={(c) => setF({ ...f, isClientFacing: !!c })} />{t('milestone.clientFacing')}</label>
          <Field label={t('common.description')} htmlFor="m-desc" optional className="sm:col-span-2"><Textarea id="m-desc" rows={3} value={f.description ?? ''} onChange={(e) => setF({ ...f, description: e.target.value })} /></Field>
          {err && <div className="sm:col-span-2"><ErrorBanner error={err} /></div>}
          <DialogFooter className="sm:col-span-2">
            <Button type="button" variant="outline" onClick={() => safe.close(onClose)}>{t('common.cancel')}</Button>
            <Button type="submit" disabled={busy}>{busy ? <><Spinner />{t('common.saving')}</> : t('common.save')}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

const itemList = 'max-h-40 space-y-1 overflow-y-auto rounded-md border bg-muted p-3 text-sm'

/** M-02..M-04 with a preview; the cascade optionally reaches tasks (packet 018). */
export function ChangeDateDialog({ m, onClose, onDone, initialDate }: { m: MilestoneRow; onClose: () => void; onDone: () => void; initialDate?: string }) {
  const [date, setDate] = useState(initialDate ?? m.date ?? '')
  const [cascade, setCascade] = useState(false)
  const [tasks, setTasks] = useState(false)
  const preview = useQuery({ queryKey: ['ms-preview', m.id, date, cascade, tasks], enabled: !!date && date !== m.date,
    queryFn: () => post(`milestones/${m.id}/change-date`, { newDate: date, cascadeDeliverables: cascade, includeTasks: tasks, dryRun: true }) })
  const pv = preview.data
  return (
    <ConfirmDialog open onOpenChange={(o) => !o && onClose()} title={t('milestone.changeDateTitle', { key: m.key, name: m.name })} reason busy={!date || date === m.date}
      confirmLabel={t('milestone.changeDate')}
      onConfirm={async (reason) => { await post(`milestones/${m.id}/change-date`, { newDate: date, reason, cascadeDeliverables: cascade, includeTasks: tasks, rowVersion: m.rowVersion }); onDone() }}>
      <div className="space-y-3 text-sm">
        <Field label={t('milestone.newDate')} htmlFor="md-date"><Input id="md-date" type="date" value={date} onChange={(e) => setDate(e.target.value)} /></Field>
        {pv && <p className="rounded-md bg-muted px-3 py-2 tabular-nums">{t('milestone.slipPreview', { from: fmtDate(pv.milestone.oldDate), to: fmtDate(pv.milestone.newDate), delta: pv.milestone.delta, slip: pv.milestone.slipDays })}</p>}
        <label className="flex items-start gap-2"><Checkbox className="mt-0.5" checked={cascade} onCheckedChange={(c) => { setCascade(!!c); if (!c) setTasks(false) }} />{t('milestone.cascade')}</label>
        {cascade && <label className="ml-6 flex items-start gap-2"><Checkbox className="mt-0.5" checked={tasks} onCheckedChange={(c) => setTasks(!!c)} />{t('milestone.cascadeTasks')}</label>}
        {pv && cascade && (
          <div className={itemList}>
            {pv.deliverables.length + pv.tasks.length === 0 ? <p className="text-muted-foreground">{t('milestone.nothingToShift')}</p> : (
              <ul className="space-y-1 tabular-nums">
                {pv.deliverables.map((x: any) => <li key={x.id}><span className="key">{x.key}</span> {x.name}: {fmtDate(x.oldDue)} → {fmtDate(x.newDue)}</li>)}
                {pv.tasks.map((x: any) => <li key={x.id} className="pl-4"><span className="key">{x.key}</span> {x.name}: {fmtDate(x.oldDue)} → {fmtDate(x.newDue)}</li>)}
              </ul>
            )}
          </div>
        )}
        {pv && pv.inconsistent.length > 0 && <p className="text-warn"><span aria-hidden>▲ </span>{t('milestone.willBeInconsistent', { n: pv.inconsistent.length })}</p>}
      </div>
    </ConfirmDialog>
  )
}

function CompleteDialog({ p, m, onClose, onDone }: { p: ProjectDetail; m: MilestoneRow; onClose: () => void; onDone: () => void }) {
  const [date, setDate] = useState(today())
  const [confirmList, setConfirmList] = useState<any[] | null>(null)
  const [phase, setPhase] = useState<{ id: string; name: string } | null>(null)
  if (phase) return (
    <ConfirmDialog open onOpenChange={(o) => { if (!o) onDone() }} title={t('milestone.advancePhase', { phase: phase.name })} body={t('milestone.advancePhaseHint')} confirmLabel={t('common.yes')}
      onConfirm={async () => { await typedRequest('patch', '/api/v1/projects/{id}', { params: { id: p.id }, body: { phaseId: phase.id }, ifMatch: p.rowVersion }); onDone() }} />
  )
  return (
    <ConfirmDialog open onOpenChange={(o) => !o && onClose()} title={t('milestone.completeTitle', { key: m.key, name: m.name })} confirmLabel={t('milestone.complete')}
      body={confirmList ? t('milestone.openDeliverables', { n: confirmList.length }) : undefined}
      onConfirm={async () => {
        try {
          const r = await post(`milestones/${m.id}/complete`, { completedDate: date, confirm: !!confirmList, rowVersion: m.rowVersion })
          toast.success(t('milestone.completed'))
          if (r.suggestPhase) setPhase(r.suggestPhase); else onDone()
        } catch (e) {
          if ((e as ApiError).code === 'confirm_open_deliverables') { setConfirmList((e as ApiError).body.deliverables); throw new ApiError(409, { detail: t('milestone.confirmAgain') }) }
          throw e
        }
      }}>
      <Field label={t('milestone.completedDate')} htmlFor="mc-date" hint={t('milestone.completedHint')}><Input id="mc-date" type="date" max={today()} value={date} onChange={(e) => setDate(e.target.value)} /></Field>
      {confirmList && <ul className={itemList}>{confirmList.map((x) => <li key={x.id}><span className="key">{x.key}</span> {x.name} · {tv(x.status)}</li>)}</ul>}
    </ConfirmDialog>
  )
}

/** M-07, E-17: cancel keeps links; the PM then retargets the deliverables in one dialog. */
function CancelDialog({ p, m, onClose, onDone }: { p: ProjectDetail; m: MilestoneRow; onClose: () => void; onDone: () => void }) {
  const [retarget, setRetarget] = useState<any[] | null>(null)
  const [target, setTarget] = useState('')
  const others = useQuery({ queryKey: ['p', p.id, 'milestones', false, ''], queryFn: () => get<MilestoneRow[]>(`projects/${p.id}/milestones`) })
  if (retarget && retarget.length > 0) return (
    <ConfirmDialog open onOpenChange={(o) => { if (!o) onDone() }} title={t('milestone.retargetTitle', { n: retarget.length })} confirmLabel={t('milestone.retarget')} busy={!target}
      onConfirm={async () => { await typedRequest('post', '/api/v1/projects/{id}/deliverables/bulk', { params: { id: p.id }, body: { ids: retarget.map((x) => x.id), operation: 'setMilestone', params: { milestoneId: target }, reason: null } }); onDone() }}>
      <ul className={itemList}>{retarget.map((x) => <li key={x.id}><span className="key">{x.key}</span> {x.name}</li>)}</ul>
      <Field label={t('milestone.retargetTo')} htmlFor="mr-target">
        <select id="mr-target" className={selectCls} value={target} onChange={(e) => setTarget(e.target.value)}>
          <option value="">{t('common.selectPlaceholder')}</option>{(others.data ?? []).filter((x) => x.id !== m.id && !x.isCancelled).map((x) => <option key={x.id} value={x.id}>{x.key} {x.name}</option>)}
        </select>
      </Field>
    </ConfirmDialog>
  )
  return (
    <ConfirmDialog open onOpenChange={(o) => !o && onClose()} destructive reason title={t('milestone.cancelTitle', { key: m.key, name: m.name })} body={t('milestone.cancelHint')}
      confirmLabel={t('milestone.cancel')}
      onConfirm={async (reason) => { const r = await post(`milestones/${m.id}/cancel`, { reason, rowVersion: m.rowVersion }); if (r.retarget?.length) setRetarget(r.retarget); else onDone() }} />
  )
}

// ---------- Milestone panel (§13.7) ----------

export function MilestonePanel({ id }: PanelProps) {
  const q = useQuery({ queryKey: ['milestone', id], queryFn: () => get(`milestones/${id}`) })
  // The project says whether a change needs a reason; asked only while pause saves exist (flag.data_entry_safety).
  const project = useProject(useFlag('data_entry_safety') ? q.data?.project.projectNumber : undefined)
  const [tab, setTab] = useState<'deliverables' | 'comments' | 'history'>('deliverables')
  const [baseline, setBaseline] = useState<'set' | 'clear' | null>(null)
  const refresh = useProjectRefresh()
  if (q.isPending) return <Loading />
  if (q.error) return <div className="p-6"><ErrorBanner error={q.error} retry={() => q.refetch()} /></div>
  const m: MilestoneRow = q.data.milestone
  const can = q.data.permissions.manage.ok
  // Typed text saves on blur only until the project says no reason is needed (FR-SAV-01).
  const needsReason = project.data?.permissions.needsReason !== false
  const save = (body: object) => patch(`milestones/${m.id}`, body, m.rowVersion).then(() => { refresh(m.projectId) })
  // Phones read the milestone; editing it is desktop/tablet only (§13.0).
  const text = (value: string | undefined, onSave: (v: string | null) => Promise<unknown>, multiline?: boolean) => (
    <DesktopOnly notice={<InlineText value={value} multiline={multiline} disabled onSave={onSave} />}><InlineText value={value} multiline={multiline} disabled={!can} needsReason={needsReason} onSave={onSave} /></DesktopOnly>
  )
  return (
    <PickerProject.Provider value={m.projectId}><div>
      <div className="space-y-2 border-b px-6 py-5">
        <div className="flex flex-wrap items-center gap-2"><Key>{m.key}</Key><StatusPill status={milestoneStatusLabel(m)} /></div>
        <h2 className="break-words text-2xl/8 font-semibold tracking-[-0.4px]">{m.isSubmission && '◆ '}{m.name}</h2>
        {m.statusReasons?.length ? <Why reasons={m.statusReasons} title={t('milestone.statusWhy', { key: m.key })}><span className="text-sm text-muted-foreground">{t('common.why')}</span></Why> : null}
      </div>
      {can && <DesktopOnly notice={<PhoneNotice className="mx-6 mt-4" />}>{null}</DesktopOnly>}
      {m.canRebaseline && <div className="flex flex-wrap gap-2 px-6 pt-3"><Button size="sm" variant="outline" onClick={() => setBaseline('set')}>{t('baseline.rebaseline')}</Button>{m.baselineDate && <Button size="sm" variant="ghost" onClick={() => setBaseline('clear')}>{t('baseline.useOriginal')}</Button>}</div>}
      <div className="px-6 py-3">
        <FieldRow label={t('common.name')}>{text(m.name, (v) => save({ name: v }))}</FieldRow>
        <FieldRow label={t('common.type')}><div className="px-2 py-1.5">{runtimeText(`mtype.${m.milestoneType}`)}</div></FieldRow>
        <FieldRow label={t('common.date')}><div className="px-2 py-1.5 tabular-nums">{fmtDate(m.date)} {m.date && <span className="text-muted-foreground">({relative(m.date)})</span>}</div></FieldRow>
        <FieldRow label={t('milestone.original')}><div className="px-2 py-1.5 tabular-nums">{fmtDate(m.originalDate)}{m.slipDays > 0 && <span className="ml-2 text-warn"><span aria-hidden>▲ </span>{t('milestone.slipped', { n: m.slipDays })}</span>}</div></FieldRow>
        <FieldRow label={t('milestone.readiness')}><div className="px-2 py-1.5 tabular-nums">{t('milestone.issuedOf', { n: m.deliverableIssued, total: m.deliverableTotal })} · {t('milestone.tasksComplete', { n: m.taskComplete, total: m.taskTotal })}</div></FieldRow>
        {m.baselineDate && <FieldRow label={t('baseline.rebaseline')}><span className="px-2 py-1.5 tabular-nums">{fmtDate(m.baselineDate)} · {t('interaction.sinceBaseline', { n: m.sinceBaselineDays ?? 0 })}</span></FieldRow>}
        <FieldRow label={t('field.CompletesPhaseId')}><div className="px-2 py-1.5">{q.data.completesPhase?.name ?? t('common.dash')}</div></FieldRow>
        <FieldRow label={t('common.description')}>{text(m.description, (v) => save({ description: v }), true)}</FieldRow>
        {m.isCancelled && <p className="mt-2 rounded-md bg-idle-bg px-3 py-2 text-sm text-idle">{t('milestone.cancelledNote', { reason: m.cancelledReason ?? '' })}</p>}
      </div>
      <TabBar tabs={[{ id: 'deliverables' as const, label: t('milestone.deliverables'), count: q.data.deliverables.length }, ...(ItemSlots.Comments ? [{ id: 'comments' as const, label: t('common.comments') }] : []), { id: 'history' as const, label: t('common.history') }]} value={tab} onChange={setTab} />
      {tab === 'deliverables' && <div className="p-6"><TargetedDeliverables milestoneId={m.id} /></div>}
      {tab === 'comments' && <CommentsSlot type="Milestone" id={m.id} projectId={m.projectId} />}
      {tab === 'history' && <HistoryList type="Milestone" id={m.id} />}
      {baseline && <ConfirmDialog open onOpenChange={(o) => !o && setBaseline(null)} title={baseline === 'clear' ? t('baseline.useOriginal') : t('baseline.rebaseline')} reason
        onConfirm={async (reason) => { await typedRequest('post', '/api/v1/milestones/{id}/rebaseline', { params: { id: m.id }, body: { reason, clear: baseline === 'clear', rowVersion: m.rowVersion } }); setBaseline(null); refresh(m.projectId) }} />}
    </div></PickerProject.Provider>
  )
}


