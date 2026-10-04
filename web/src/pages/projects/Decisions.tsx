import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Building2, Check, ChevronDown, ChevronRight, Download, Lock, Plus, Scale, Trash2, Users, X } from 'lucide-react'
import { Fragment, cloneElement, useMemo, useState, type CSSProperties, type ReactElement, type ReactNode } from 'react'
import { Link, useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { ActiveFilters, ChipToggle, ConfirmDialog, Empty, ErrorBanner, Field, FilterBar, Loading, Missing, Notice, Page, Segmented, Spinner, TableRegion, selectCls } from '@/components/hub/common'
import { FieldRow, HistoryList, InlineDate, InlineSelect, InlineText, TabBar } from '@/components/hub/fields'
import { PANELS, useItemPanel, type PanelProps } from '@/components/hub/panel-host'
import { Avatar, PeoplePicker } from '@/components/hub/people'
import { Chip, Key, StatusPill } from '@/components/hub/pills'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { useProjectRefresh } from '@/hooks/data'
import { ApiError, del, download, get, patch, post, qs } from '@/lib/api'
import { addDays, fmtDate, fmtTime, today } from '@/lib/format'
import { plural, t, tv } from '@/lib/i18n'
import type { Page as PageOf, ProjectDetail } from '@/lib/types'
import { cn } from '@/lib/utils'
import type { MilestoneRow } from './Milestones'
import type { DeliverableRow } from './Deliverables'
import { CommentsSlot, ItemSlots } from './slots-items'
import { errorText, type TaskRow } from './Tasks'
import { useCurrentProject } from './ProjectLayout'
import { ExportMenu } from '@/components/hub/export'
import { useTable, type Column } from '@/components/hub/table'
import { ViewMenu } from '@/components/hub/views'

export interface DecisionRow {
  id: string; projectId: string; key: string; subject: string; status: string; requestedById: string; requestedByName?: string
  ownerUserId?: string; ownerExternalPartyId?: string; ownerName?: string; ownerOrganisation?: string; ownerIsClient: boolean
  dateRequested: string; requiredByDate: string; originalRequiredByDate: string; impactLevel: string; impactDescription: string
  decisionText?: string; decisionDate?: string; rowVersion: number; daysUntil?: number | null
  isOverdue: boolean; daysOverdue: number; isDueSoon: boolean; isInactiveOwner: boolean; blockingTaskIds: string[]
  deliverables: { id: string; key: string; name: string }[]; milestones: { id: string; key: string; name: string }[]
}
interface Perm { ok: boolean; reason?: string | null }
interface LinkRow { id: string; targetType: string; targetId: string; relation: string; key: string; name: string; status?: string; date?: string; person?: string }
interface DecisionDetail {
  decision: DecisionRow; description: string; deferralReason?: string; cancelledReason?: string; decidedBy?: string
  project: { id: string; projectNumber: string; name: string; status: string }
  links: LinkRow[]
  permissions: { edit: Perm; transitions: { to: string; ok: boolean; reason?: string | null }[]; comment: boolean }
}
export interface Party { id: string; name: string; organisation?: string; email?: string; role?: string; isClient: boolean; notes?: string; isActive: boolean; rowVersion: number }

const STATUSES = ['Pending', 'Under Review', 'Decided', 'Deferred', 'Cancelled']
const OPEN = 'Pending,Under Review,Deferred'
const IMPACTS = ['High', 'Medium', 'Low']
const IMPACT_TONE = { High: 'bad', Medium: 'warn', Low: 'idle' } as const
const FILTERS = ['q', 'status', 'ownerId', 'ownerType', 'impact', 'requiredFrom', 'requiredTo', 'blocking', 'indicator'] as const

const useParties = (projectId: string) => useQuery({ queryKey: ['p', projectId, 'parties'], queryFn: () => get<Party[]>(`projects/${projectId}/external-parties`), enabled: !!projectId })

// ---------- Register presentation shared by decisions, deliverables, risks, issues and meeting actions (design §6) ----------

/** The row whose item panel is open: a tint plus a left bar, never the tint alone. */
export const SELECTED_ROW = 'bg-accent shadow-[inset_3px_0_0_var(--primary)] hover:bg-accent'
/** The row's identity (title, subject, name) that opens its panel. */
export const TITLE_LINK = 'break-words text-left font-medium text-primary underline-offset-4 hover:underline'

/** A person in a register: their stable pastel avatar and name; an unknown person shows as Missing. */
export function Person({ id, name }: { id?: string | null; name?: string | null }) {
  if (!name) return <Missing />
  return <span className="inline-flex items-center gap-2"><Avatar id={id} name={name} />{name}</span>
}

/** A calendar date in tabular figures; no date shows as Missing. */
export function DateText({ date }: { date?: string | null }) {
  return date ? <span className="whitespace-nowrap tabular-nums">{fmtDate(date)}</span> : <Missing />
}

/** A useTable header cell that follows its column's alignment: dates and numbers are right-aligned (design §6 Tables).
 *  The shared hook does not pass a column's alignment to its header yet; delete this once table.tsx does. */
export function HeadCell({ th, right }: { th: ReactElement; right?: boolean }) {
  return right ? cloneElement(th as ReactElement<{ style?: CSSProperties }>, { style: { textAlign: 'right' } }) : th
}

/** The labelled header row of a group in a grouped register table. */
export function GroupRow({ span, label, count }: { span: number; label: ReactNode; count: number }) {
  return (
    <tr className="border-t">
      <th colSpan={span} scope="rowgroup" className="bg-muted px-(--cell-px) py-2 text-left font-semibold">
        {label}<span className="ml-2 rounded-md bg-card px-2 py-0.5 text-xs font-medium text-muted-foreground tabular-nums">{count}</span>
      </th>
    </tr>
  )
}

/** Phones read a register as cards (§13.0 Responsive, design §5): each row's visible columns as labelled values. */
export function RegisterCards<T extends { id: string }>({ groups, columns, current }: { groups: { label: string; rows: T[] }[]; columns: Column<T>[]; current: (r: T) => boolean }) {
  return (
    <div className="space-y-4 md:hidden">
      {groups.map((g, i) => (
        <div key={`${i}-${g.label}`} className="space-y-2">
          {g.label && <h3 className="text-sm font-semibold">{g.label}<span className="ml-2 rounded-md bg-secondary px-2 py-0.5 text-xs font-medium text-muted-foreground tabular-nums">{g.rows.length}</span></h3>}
          <ul className="space-y-2">
            {g.rows.map((r) => (
              <li key={r.id} className={cn('rounded-lg border bg-card p-4', current(r) && SELECTED_ROW)}>
                <dl className="grid grid-cols-[minmax(5.5rem,auto)_minmax(0,1fr)] items-start gap-x-3 gap-y-1.5 text-sm">
                  {columns.map((c) => <Fragment key={c.id}><dt className="text-muted-foreground">{c.label}</dt><dd className="min-w-0 break-words">{c.cell(r)}</dd></Fragment>)}
                </dl>
              </li>
            ))}
          </ul>
        </div>
      ))}
    </div>
  )
}

/** An item panel's header: key and state chips, the 24 px title, then the item's own actions. */
export function PanelHead({ meta, title, children }: { meta: ReactNode; title: ReactNode; children?: ReactNode }) {
  return (
    <div className="space-y-3 p-4">
      <div className="flex flex-wrap items-center gap-2">{meta}</div>
      <h2 className="break-words text-2xl/8 font-semibold tracking-[-0.4px]">{title}</h2>
      {children}
    </div>
  )
}

/** Panel fields grouped by purpose under a small heading (design §6 Detail panel). */
export function FieldGroup({ title, children }: { title: ReactNode; children: ReactNode }) {
  return (
    <section className="border-t p-4">
      <h3 className="mb-2 text-sm font-semibold">{title}</h3>
      {children}
    </section>
  )
}

function Impact({ level }: { level: string }) {
  return <Chip tone={IMPACT_TONE[level as keyof typeof IMPACT_TONE] ?? 'idle'}>{tv(level)}</Chip>
}

/** Owner with external parties set apart by icon and organisation (§13.8 UX). */
function Owner({ d }: { d: DecisionRow }) {
  if (!d.ownerExternalPartyId) return <Person id={d.ownerUserId} name={d.ownerName} />
  return (
    <span className="inline-flex items-center gap-1.5" title={d.ownerIsClient ? t('decision.clientOwner') : t('decision.externalOwner')}>
      <Building2 className="size-4 shrink-0 text-muted-foreground" aria-hidden />
      <span>{d.ownerName}</span>{d.ownerOrganisation && <span className="text-muted-foreground">· {d.ownerOrganisation}</span>}
      <span className="sr-only">({d.ownerIsClient ? t('decision.clientOwner') : t('decision.externalOwner')})</span>
    </span>
  )
}

function Due({ d }: { d: DecisionRow }) {
  if (d.isOverdue) return <Chip tone="bad">{t('ind.overdueD', { n: d.daysOverdue })}</Chip>
  if (d.daysUntil == null) return <Missing />
  const text = d.daysUntil === 0 ? t('common.today') : d.daysUntil < 0 ? t('ind.overdueD', { n: -d.daysUntil }) : t('decision.inDays', { n: d.daysUntil })
  return d.isDueSoon ? <Chip tone="warn">{text}</Chip> : <span className="tabular-nums">{text}</span>
}

function Blocking({ d }: { d: DecisionRow }) {
  if (!d.blockingTaskIds.length) return <span className="text-muted-foreground">{t('common.dash')}</span>
  return <Link to={`../tasks?ids=${d.blockingTaskIds.join(',')}`} relative="path" className="inline-flex items-center gap-1 font-medium text-bad underline underline-offset-4">
    <span aria-hidden>■</span>{plural(d.blockingTaskIds.length, 'decision.task1', 'wc.tasksN')}</Link>
}

/** Decision Register (§13.8): an agenda for the client call, with filters in the URL and rows that expand to linked items. */
export function DecisionsTab() {
  const p = useCurrentProject()
  const [sp, setSp] = useSearchParams()
  const openPanel = useItemPanel()
  const [dialog, setDialog] = useState<'new' | 'parties' | 'client' | null>(null)
  const [open, setOpen] = useState<Set<string>>(new Set())
  const set = (k: string, v?: string | null) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); setSp(n, { replace: true }) }
  const filters = Object.fromEntries(FILTERS.map((k) => [k, sp.get(k)]))
  const q = useQuery({ queryKey: ['p', p.id, 'decisions', filters], queryFn: () => get<DecisionRow[]>(`projects/${p.id}/decisions${qs(filters)}`) })
  const panel = sp.get('panel')
  const table = useTable<DecisionRow>('hub.decisionColumns', [
    { id: 'key', label: t('milestone.key'), fixed: true, sort: (d) => d.key, className: 'whitespace-nowrap', cell: (d) => <Key>{d.key}</Key> },
    { id: 'subject', label: t('decision.subject'), fixed: true, sort: (d) => d.subject.toLowerCase(), className: 'min-w-[12rem]',
      cell: (d) => <button className={TITLE_LINK} aria-current={panel === `Decision:${d.id}` || undefined} onClick={() => openPanel('Decision', d.id)}>{d.subject}</button> },
    { id: 'owner', label: t('common.owner'), sort: (d) => d.ownerName, className: 'whitespace-nowrap',
      cell: (d) => <span className="flex flex-wrap items-center gap-x-2 gap-y-1"><Owner d={d} />{d.isInactiveOwner && <Chip tone="bad">{t('decision.inactiveOwner')}</Chip>}</span> },
    { id: 'requestedBy', label: t('decision.requestedBy'), sort: (d) => d.requestedByName, className: 'whitespace-nowrap', cell: (d) => <Person id={d.requestedById} name={d.requestedByName} /> },
    { id: 'requested', label: t('decision.requested'), sort: (d) => d.dateRequested, className: 'text-right', cell: (d) => <DateText date={d.dateRequested} /> },
    { id: 'requiredBy', label: t('decision.requiredBy'), sort: (d) => d.requiredByDate, className: 'text-right', cell: (d) => <DateText date={d.requiredByDate} /> },
    { id: 'days', label: t('decision.daysTo'), sort: (d) => (d.isOverdue ? -d.daysOverdue : d.daysUntil), className: 'whitespace-nowrap text-right', cell: (d) => <Due d={d} /> },
    { id: 'impact', label: t('decision.impact'), sort: (d) => IMPACTS.indexOf(d.impactLevel), cell: (d) => <Impact level={d.impactLevel} /> },
    { id: 'status', label: t('common.status'), sort: (d) => d.status, cell: (d) => <StatusPill status={d.status} /> },
    { id: 'blocking', label: t('decision.blocking'), sort: (d) => -d.blockingTaskIds.length, className: 'whitespace-nowrap text-right', cell: (d) => <Blocking d={d} /> },
    { id: 'linked', label: t('decision.linked'), cell: (d) => <span className="flex flex-wrap gap-1">
      {[...d.deliverables.map((x) => ['Deliverable', x] as const), ...d.milestones.map((x) => ['Milestone', x] as const)].map(([type, x]) => (
        <button key={x.id} title={x.name} onClick={() => openPanel(type, x.id)} className="hover:underline"><Key>{x.key}</Key></button>))}</span> },
  ], q.data ?? [], (d) => [d.requiredByDate, d.key])
  const rows = table.sorted
  const owners = useMemo(() => {
    const m = new Map<string, string>()
    for (const d of q.data ?? []) { const id = d.ownerUserId ?? d.ownerExternalPartyId; if (id && d.ownerName) m.set(id, d.ownerName) }
    return [...m.entries()].sort((a, b) => a[1].localeCompare(b[1]))
  }, [q.data])
  const can = p.permissions.raiseRegister
  const toggle = (id: string) => { const n = new Set(open); if (n.has(id)) n.delete(id); else n.add(id); setOpen(n) }
  const active = FILTERS.some((k) => sp.has(k))
  const clear = () => setSp(new URLSearchParams(), { replace: true })
  const view = sp.get('view') === 'log' ? 'log' : 'register'
  // Active filters as removable tokens (§13.0 Filters), including those that arrive by link (indicator).
  const tokens = [
    filters.q && { key: 'q', label: t('common.search'), value: filters.q },
    filters.status && { key: 'status', label: t('common.status'), value: filters.status === OPEN ? t('decision.openStatuses') : tv(filters.status) },
    filters.ownerType && { key: 'ownerType', label: t('decision.ownerType'), value: t(`decision.ownerType.${filters.ownerType}`) },
    filters.ownerId && { key: 'ownerId', label: t('common.owner'), value: owners.find(([id]) => id === filters.ownerId)?.[1] ?? <Missing /> },
    filters.impact && { key: 'impact', label: t('decision.impact'), value: tv(filters.impact) },
    filters.requiredFrom && { key: 'requiredFrom', label: t('decision.requiredFrom'), value: fmtDate(filters.requiredFrom) },
    filters.requiredTo && { key: 'requiredTo', label: t('decision.requiredTo'), value: fmtDate(filters.requiredTo) },
    filters.blocking && { key: 'blocking', label: t('decision.blockingWork'), value: filters.blocking === 'true' ? t('common.yes') : t('common.no') },
    filters.indicator && { key: 'indicator', label: t('deliverable.indicator'), value: t(`decision.ind.${filters.indicator}`) },
  ].filter(Boolean) as { key: string; label: string; value: ReactNode }[]
  return (
    <Page title={t('ptab.decisions')} subtitle={t('decision.subtitle')}
      actions={<>
        {view === 'register' && <>{table.menu}<ViewMenu listType="decisions" projectId={p.id} extra={() => ({ cols: table.colsParam })} /></>}
        <ExportMenu path={`projects/${p.id}/decisions/export`} params={filters} name={`${p.projectNumber}-decisions`} />
        <Button variant="outline" onClick={() => setDialog('client')}><Download className="size-4" />{t('decision.clientExport')}</Button>
        <Button variant="outline" onClick={() => setDialog('parties')}><Users className="size-4" />{t('party.title')}</Button>
        {can.ok && <Button onClick={() => setDialog('new')}><Plus className="size-4" />{t('decision.new')}</Button>}
      </>}>
      {!can.ok && can.reason && <Notice icon={Lock} title={can.reason} />}
      <div><Segmented label={t('decision.view')} value={view} onChange={(v) => set('view', v === 'log' ? v : null)}
        options={(['register', 'log'] as const).map((v) => ({ value: v, label: t(`decision.view.${v}`) }))} /></div>
      {view === 'log' ? <DecisionLog projectId={p.id} /> : <>
      <FilterBar>
        <div className="flex flex-wrap items-end gap-3">
          <Field label={t('common.search')} htmlFor="decision-search" className="w-full sm:w-56">
            <Input id="decision-search" type="search" value={filters.q ?? ''} onChange={(e) => set('q', e.target.value)} />
          </Field>
          <Field label={t('common.status')} htmlFor="decision-status" className="w-full sm:w-56">
            <select id="decision-status" className={selectCls} value={filters.status ?? ''} onChange={(e) => set('status', e.target.value)}>
              <option value="">{t('decision.anyStatus')}</option><option value={OPEN}>{t('decision.openStatuses')}</option>
              {STATUSES.map((s) => <option key={s} value={s}>{tv(s)}</option>)}
            </select>
          </Field>
          <Field label={t('decision.ownerType')} htmlFor="decision-owner-type" className="w-full sm:w-44">
            <select id="decision-owner-type" className={selectCls} value={filters.ownerType ?? ''} onChange={(e) => set('ownerType', e.target.value)}>
              <option value="">{t('decision.anyOwnerType')}</option>{['internal', 'external', 'client'].map((x) => <option key={x} value={x}>{t(`decision.ownerType.${x}`)}</option>)}
            </select>
          </Field>
          <Field label={t('common.owner')} htmlFor="decision-owner" className="w-full sm:w-48">
            <select id="decision-owner" className={selectCls} value={filters.ownerId ?? ''} onChange={(e) => set('ownerId', e.target.value)}>
              <option value="">{t('decision.anyOwner')}</option>{owners.map(([id, name]) => <option key={id} value={id}>{name}</option>)}
            </select>
          </Field>
          <Field label={t('decision.impact')} htmlFor="decision-impact" className="w-full sm:w-40">
            <select id="decision-impact" className={selectCls} value={filters.impact ?? ''} onChange={(e) => set('impact', e.target.value)}>
              <option value="">{t('decision.anyImpact')}</option>{IMPACTS.map((x) => <option key={x} value={x}>{tv(x)}</option>)}
            </select>
          </Field>
          <Field label={t('decision.requiredFrom')} htmlFor="decision-required-from" className="w-full sm:w-44">
            <Input id="decision-required-from" type="date" value={filters.requiredFrom ?? ''} onChange={(e) => set('requiredFrom', e.target.value)} />
          </Field>
          <Field label={t('decision.requiredTo')} htmlFor="decision-required-to" className="w-full sm:w-44">
            <Input id="decision-required-to" type="date" value={filters.requiredTo ?? ''} onChange={(e) => set('requiredTo', e.target.value)} />
          </Field>
        </div>
        <div className="flex flex-wrap items-center gap-2" role="group" aria-label={t('task.quickFilters')}>
          <ChipToggle on={filters.blocking === 'true'} onClick={() => set('blocking', filters.blocking === 'true' ? null : 'true')}>{t('decision.blockingWork')}</ChipToggle>
        </div>
        <ActiveFilters tokens={tokens} onRemove={(k) => set(k, null)} onClear={clear} />
      </FilterBar>
      {q.isPending ? <div className="rounded-lg border bg-card"><Loading rows={6} /></div> : q.error ? <ErrorBanner error={q.error} retry={() => q.refetch()} /> : rows.length === 0 ? (
        <div className="rounded-lg border bg-card">{active
          ? <Empty title={t('decision.noMatch')} action={<Button variant="outline" onClick={clear}>{t('filters.clear')}</Button>}>{t('register.noMatchHint')}</Empty>
          : <Empty action={can.ok && <Button variant="outline" onClick={() => setDialog('new')}><Plus className="size-4" />{t('decision.new')}</Button>}>{t('decision.empty')}</Empty>}</div>
      ) : (<>
        <RegisterCards groups={[{ label: '', rows }]} columns={table.visible} current={(d) => panel === `Decision:${d.id}`} />
        <TableRegion className="hidden md:block">
          <table className="w-full text-sm">
            <caption className="sr-only">{t('ptab.decisions')}</caption>
            <thead className="bg-muted text-left text-muted-foreground">
              <tr><th scope="col" className="w-12"><span className="sr-only">{t('common.details')}</span></th>
                {table.visible.map((c) => <HeadCell key={c.id} th={table.header(c)} right={c.className?.includes('text-right')} />)}</tr>
            </thead>
            <tbody>
              {rows.map((d) => (
                <Fragment key={d.id}>
                  <tr className={cn('border-t hover:bg-muted', panel === `Decision:${d.id}` && SELECTED_ROW)}>
                    <td className="px-2"><button className="grid size-(--control-row-h) place-items-center rounded-md hover:bg-secondary" aria-expanded={open.has(d.id)} aria-label={t('decision.showLinked', { key: d.key })} onClick={() => toggle(d.id)}>
                      {open.has(d.id) ? <ChevronDown className="size-4" /> : <ChevronRight className="size-4" />}</button></td>
                    {table.visible.map((c) => table.cell(c, d))}
                  </tr>
                  {open.has(d.id) && <tr className="border-t bg-muted"><td /><td colSpan={table.visible.length} className="px-(--cell-px) py-3"><LinkedItems id={d.id} /></td></tr>}
                </Fragment>
              ))}
            </tbody>
          </table>
        </TableRegion>
      </>)}
      </>}
      {dialog === 'new' && <DecisionForm p={p} onClose={() => setDialog(null)} />}
      {dialog === 'parties' && <PartiesDialog p={p} onClose={() => setDialog(null)} />}
      {dialog === 'client' && <ClientExportDialog p={p} onClose={() => setDialog(null)} />}
    </Page>
  )
}

interface LogEntry {
  decisionId: string; itemKey: string; subject: string; kind: string; occurredAt: string; recordedBy?: string; owner?: string
  text?: string; decisionDate?: string; newRequiredBy?: string
}

/** Decision log (FR-003): what was decided, deferred, cancelled or reopened, newest first, with the text or reason. */
function DecisionLog({ projectId }: { projectId: string }) {
  const q = useQuery({ queryKey: ['p', projectId, 'decision-log'], queryFn: () => get<LogEntry[]>(`projects/${projectId}/decision-log`) })
  const openPanel = useItemPanel()
  if (q.isPending) return <div className="rounded-lg border bg-card"><Loading rows={6} /></div>
  if (q.error) return <ErrorBanner error={q.error} retry={() => q.refetch()} />
  if (!q.data.length) return <div className="rounded-lg border bg-card"><Empty>{t('decision.logEmpty')}</Empty></div>
  const detail = (x: LogEntry) => [
    x.kind === 'Decided' && x.decisionDate && t('decision.log.decidedOn', { date: fmtDate(x.decisionDate) }),
    x.kind === 'Deferred' && x.newRequiredBy && t('decision.log.nowRequired', { date: fmtDate(x.newRequiredBy) }),
    x.recordedBy && t('decision.log.by', { who: x.recordedBy }), x.owner && t('decision.log.owner', { who: x.owner }),
  ].filter(Boolean).join(' · ')
  return (
    <ol className="divide-y rounded-lg border bg-card" aria-label={t('decision.view.log')}>
      {q.data.map((x) => (
        <li key={`${x.decisionId}-${x.occurredAt}-${x.kind}`} className="grid gap-1 px-5 py-4 text-sm sm:grid-cols-[10rem_1fr] sm:gap-4">
          <time dateTime={x.occurredAt} className="text-xs/[18px] text-muted-foreground tabular-nums sm:pt-0.5">{fmtTime(x.occurredAt)}</time>
          <div className="min-w-0 space-y-1.5">
            <div className="flex flex-wrap items-center gap-2"><StatusPill status={x.kind} /><Key>{x.itemKey}</Key>
              <button className={TITLE_LINK} onClick={() => openPanel('Decision', x.decisionId)}>{x.subject}</button></div>
            {x.text && <p className="whitespace-pre-wrap">{x.text}</p>}
            <p className="text-xs/[18px] text-muted-foreground">{detail(x)}</p>
          </div>
        </li>
      ))}
    </ol>
  )
}

/** Open decisions the client (or one chosen party) owns, as a file for the client call — nothing internal (FR-001). */
function ClientExportDialog({ p, onClose }: { p: ProjectDetail; onClose: () => void }) {
  const parties = useParties(p.id)
  const [party, setParty] = useState('')
  const [err, setErr] = useState<unknown>(null)
  const [busy, setBusy] = useState(false)
  const clients = (parties.data ?? []).filter((x) => x.isClient)
  const run = async (format: 'csv' | 'xlsx') => {
    setErr(null); setBusy(true)
    try {
      await download(`/api/v1/projects/${p.id}/decisions/client-export${qs({ partyId: party, format })}`, `${p.projectNumber}-client-decisions.${format}`)
      onClose()
    } catch (e) { setErr(e) } finally { setBusy(false) }
  }
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent>
        <DialogHeader><DialogTitle>{t('decision.clientExport')}</DialogTitle><DialogDescription>{t('decision.clientExportHint')}</DialogDescription></DialogHeader>
        <Field label={t('decision.clientExportFor')} htmlFor="ce-party">
          <select id="ce-party" className={selectCls} value={party} onChange={(e) => setParty(e.target.value)}>
            <option value="">{clients.length ? t('decision.allClients', { names: clients.map((x) => x.name).join(', ') }) : t('decision.noClientParty')}</option>
            {(parties.data ?? []).map((x) => <option key={x.id} value={x.id}>{x.name}{x.organisation ? ` · ${x.organisation}` : ''}</option>)}
          </select>
        </Field>
        {err != null && <ErrorBanner error={err} />}
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button variant="outline" disabled={busy} onClick={() => run('csv')}>{t('export.csv')}</Button>
          <Button disabled={busy} onClick={() => run('xlsx')}>{busy && <Spinner />}{t('export.xlsx')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

function useDecision(id: string) {
  return useQuery({ queryKey: ['decision', id], queryFn: () => get<DecisionDetail>(`decisions/${id}`) })
}

function LinkedItems({ id, canRemove }: { id: string; canRemove?: boolean }) {
  const q = useDecision(id)
  const openPanel = useItemPanel()
  const done = useDecisionRefresh()
  if (q.isPending) return <Loading rows={2} />
  if (q.error) return <ErrorBanner error={q.error} />
  const links = q.data.links
  if (!links.length) return <p className="py-2 text-sm text-muted-foreground">{t('decision.noLinks')}</p>
  return (
    <table className="w-full text-sm">
      <tbody>
        {links.map((l) => (
          <tr key={l.id} className="border-b last:border-0">
            <td className="py-1.5 pr-3 text-xs/[18px] text-muted-foreground">{t(`decision.rel.${l.relation}`)}</td>
            <td className="whitespace-nowrap py-1.5 pr-3"><Key>{l.key}</Key></td>
            <td className="py-1.5 pr-3"><button className="break-words text-left hover:underline" onClick={() => openPanel(l.targetType, l.targetId)}>{l.name}</button></td>
            <td className="py-1.5 pr-3">{l.status && <StatusPill status={l.status} />}</td>
            <td className="whitespace-nowrap py-1.5 pr-3">{l.person}</td>
            <td className="py-1.5 pr-3"><DateText date={l.date} /></td>
            <td className="py-1 text-right">{canRemove && <Button variant="ghost" size="icon-sm" aria-label={t('decision.unlink', { key: l.key })}
              onClick={async () => { try { await del(`item-links/${l.id}`); done(q.data.decision.projectId, id) } catch (e) { toast.error(errorText(e)) } }}><Trash2 className="size-4" /></Button>}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

function useDecisionRefresh() {
  const qc = useQueryClient()
  const refresh = useProjectRefresh()
  return (projectId: string, id?: string) => {
    if (id) qc.invalidateQueries({ queryKey: ['decision', id] })
    qc.invalidateQueries({ queryKey: ['coordination'] })
    refresh(projectId)
  }
}

// ---------- Owner choice: a person or an external party (DEC-01) ----------

function OwnerField({ p, user, userName, party, onChange, error }: {
  p: ProjectDetail; user?: string | null; userName?: string | null; party?: string | null; onChange: (v: { user: string | null; userName?: string | null; party: string | null }) => void; error?: string[]
}) {
  const parties = useParties(p.id)
  const [kind, setKind] = useState<'person' | 'party'>(party ? 'party' : 'person')
  const [adding, setAdding] = useState(false)
  const activeParties = (parties.data ?? []).filter((x) => x.isActive)
  return (
    <fieldset className="space-y-1.5 sm:col-span-2">
      <legend className="text-sm font-medium">{t('common.owner')}</legend>
      <div className="flex gap-4 text-sm" role="radiogroup">
        {(['person', 'party'] as const).map((k) => (
          <label key={k} className="flex items-center gap-1.5"><input type="radio" name="owner-kind" checked={kind === k} onChange={() => { setKind(k); onChange({ user: null, party: null }) }} />{t(`decision.owner.${k}`)}</label>
        ))}
      </div>
      {kind === 'person' ? <PeoplePicker value={user} valueName={userName} onChange={(id, person) => onChange({ user: id, userName: person?.displayName, party: null })} label={t('decision.owner.person')} /> : (
        <div className="flex gap-2">
          <select className={selectCls} value={party ?? ''} onChange={(e) => onChange({ user: null, party: e.target.value || null })} aria-label={t('decision.owner.party')}>
            <option value="">{t('decision.chooseParty')}</option>
            {activeParties.map((x) => <option key={x.id} value={x.id}>{x.name}{x.organisation ? ` · ${x.organisation}` : ''}{x.isClient ? ` (${t('party.client')})` : ''}</option>)}
          </select>
          <Button type="button" variant="outline" onClick={() => setAdding(true)}><Plus className="size-4" />{t('party.new')}</Button>
        </div>
      )}
      {kind === 'party' && <p className="text-xs/[18px] text-muted-foreground">{t('decision.noExternalEmail')}</p>}
      {error?.map((e) => <p key={e} className="text-xs/[18px] text-bad" role="alert">{e}</p>)}
      {adding && <PartyForm p={p} onClose={(created) => { setAdding(false); if (created) onChange({ user: null, party: created.id }) }} />}
    </fieldset>
  )
}

// ---------- Raise a decision (FR-001) ----------

export type Pick = { targetType: 'Task' | 'Deliverable' | 'Milestone'; targetId: string; key: string; name: string }

function DecisionForm({ p, onClose }: { p: ProjectDetail; onClose: () => void }) {
  const done = useDecisionRefresh()
  const openPanel = useItemPanel()
  const [f, setF] = useState<Record<string, any>>({ dateRequested: today(), impactLevel: 'Medium' })
  const [links, setLinks] = useState<Pick[]>([])
  const [err, setErr] = useState<ApiError | null>(null)
  const [busy, setBusy] = useState(false)
  const submit = async () => {
    setErr(null); setBusy(true)
    try {
      const r = await post(`projects/${p.id}/decisions`, { ...f, ownerUserId: f.ownerUserId || null, ownerExternalPartyId: f.ownerExternalPartyId || null,
        requestedById: f.requestedById || null, links: links.map((l) => ({ targetType: l.targetType, targetId: l.targetId })) })
      toast.success(t('decision.raised', { key: r.key }))
      done(p.id); onClose(); openPanel('Decision', r.id)
    } catch (e) { setErr(e as ApiError) } finally { setBusy(false) }
  }
  const fe = err?.fieldErrors ?? {}
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
        <DialogHeader><DialogTitle>{t('decision.new')}</DialogTitle><DialogDescription>{t('decision.newHint')}</DialogDescription></DialogHeader>
        <form className="grid gap-3 sm:grid-cols-2" onSubmit={(e) => { e.preventDefault(); submit() }}>
          <Field label={t('decision.subject')} htmlFor="d-subject" error={fe.subject} className="sm:col-span-2"><Input id="d-subject" required value={f.subject ?? ''} onChange={(e) => setF({ ...f, subject: e.target.value })} /></Field>
          <Field label={t('common.description')} htmlFor="d-desc" error={fe.description} hint={t('decision.descriptionHint')} className="sm:col-span-2">
            <Textarea id="d-desc" required rows={3} value={f.description ?? ''} onChange={(e) => setF({ ...f, description: e.target.value })} />
          </Field>
          <OwnerField p={p} user={f.ownerUserId} userName={f.ownerName} party={f.ownerExternalPartyId} error={[...(fe.ownerUserId ?? []), ...(fe.ownerExternalPartyId ?? [])]}
            onChange={(v) => setF({ ...f, ownerUserId: v.user, ownerName: v.userName, ownerExternalPartyId: v.party })} />
          <Field label={t('decision.requestedBy')} htmlFor="d-req" error={fe.requestedById}>
            <PeoplePicker id="d-req" value={f.requestedById} valueName={f.requestedByName} placeholder={t('decision.me')} onChange={(id, person) => setF({ ...f, requestedById: id, requestedByName: person?.displayName })} />
          </Field>
          <Field label={t('decision.requested')} htmlFor="d-dr" error={fe.dateRequested}><Input id="d-dr" type="date" required value={f.dateRequested ?? ''} onChange={(e) => setF({ ...f, dateRequested: e.target.value })} /></Field>
          <Field label={t('decision.requiredBy')} htmlFor="d-rb" error={fe.requiredByDate}><Input id="d-rb" type="date" required value={f.requiredByDate ?? ''} onChange={(e) => setF({ ...f, requiredByDate: e.target.value })} /></Field>
          <Field label={t('decision.impact')} htmlFor="d-imp" error={fe.impactLevel}>
            <select id="d-imp" className={selectCls} value={f.impactLevel} onChange={(e) => setF({ ...f, impactLevel: e.target.value })}>{IMPACTS.map((x) => <option key={x} value={x}>{tv(x)}</option>)}</select>
          </Field>
          <Field label={t('decision.impactDescription')} htmlFor="d-impd" error={fe.impactDescription} hint={t('decision.impactHint')} className="sm:col-span-2">
            <Input id="d-impd" required value={f.impactDescription ?? ''} onChange={(e) => setF({ ...f, impactDescription: e.target.value })} />
          </Field>
          <div className="space-y-1.5 sm:col-span-2">
            <div className="text-sm font-medium">{t('decision.links')}</div>
            <p className="text-xs/[18px] text-muted-foreground">{t('decision.linksHint')}</p>
            {links.length > 0 && <ul className="flex flex-wrap gap-1.5">{links.map((l) => (
              <li key={l.targetId} className="flex items-center gap-1.5 rounded-md border bg-card py-0.5 pl-2 pr-0.5 text-xs/[18px]">
                <span className="text-muted-foreground">{t(l.targetType === 'Task' ? 'decision.rel.blocked_by_decision' : 'decision.rel.related')}</span><Key>{l.key}</Key>{l.name}
                <button type="button" className="grid size-6 place-items-center rounded-md hover:bg-muted" aria-label={t('common.remove')} onClick={() => setLinks(links.filter((x) => x.targetId !== l.targetId))}><X className="size-3.5" aria-hidden /></button>
              </li>))}</ul>}
            <ItemPicker projectId={p.id} exclude={links.map((l) => l.targetId)} onPick={(x) => setLinks([...links, x])} />
          </div>
          {err && !Object.keys(fe).length && <div className="sm:col-span-2"><ErrorBanner error={err} /></div>}
          <DialogFooter className="sm:col-span-2"><Button type="button" variant="outline" onClick={onClose}>{t('common.cancel')}</Button><Button type="submit" disabled={busy}>{busy && <Spinner />}{busy ? t('common.saving') : t('decision.raise')}</Button></DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

/** Search across the project's open tasks, deliverables and milestones to link them. */
export function ItemPicker({ projectId, exclude, onPick, selected, onPickAll }: {
  projectId: string; exclude: string[]; onPick: (x: Pick) => void; selected?: string[]; onPickAll?: (xs: Pick[]) => void
}) {
  const [term, setTerm] = useState('')
  const tasks = useQuery({ queryKey: ['p', projectId, 'tasks-pick', term], queryFn: () => get<PageOf<TaskRow>>(`projects/${projectId}/tasks${qs({ q: term, open: true, pageSize: 50 })}`) })
  const dels = useQuery({ queryKey: ['p', projectId, 'deliverables', {}], queryFn: () => get<DeliverableRow[]>(`projects/${projectId}/deliverables`) })
  const ms = useQuery({ queryKey: ['p', projectId, 'milestones', false, ''], queryFn: () => get<MilestoneRow[]>(`projects/${projectId}/milestones`) })
  const match = (x: { key: string; name: string }) => !term || `${x.key} ${x.name}`.toLowerCase().includes(term.toLowerCase())
  const options: Pick[] = [
    ...(tasks.data?.items ?? []).map((x) => ({ targetType: 'Task' as const, targetId: x.id, key: x.key, name: x.name })),
    ...(dels.data ?? []).filter(match).map((x) => ({ targetType: 'Deliverable' as const, targetId: x.id, key: x.key, name: x.name })),
    ...(ms.data ?? []).filter((x) => !x.isCancelled && match(x)).map((x) => ({ targetType: 'Milestone' as const, targetId: x.id, key: x.key, name: x.name })),
  ].filter((x) => !exclude.includes(x.targetId))
  const shown = options.slice(0, 60)
  return (
    <div className="rounded-md border border-input bg-card">
      <div className="flex items-center border-b">
        <Input type="search" className="flex-1 rounded-b-none border-0" placeholder={t('decision.searchItems')} value={term} onChange={(e) => setTerm(e.target.value)} aria-label={t('decision.searchItems')} />
        {onPickAll && shown.length > 0 && <button type="button" className="shrink-0 px-3 text-sm text-primary underline-offset-4 hover:underline" onClick={() => onPickAll(shown)}>{t('decision.selectShown', { n: shown.length })}</button>}
      </div>
      <div className="max-h-48 overflow-y-auto" role="listbox" aria-multiselectable={selected ? true : undefined} aria-label={t('decision.links')}>
        {tasks.isPending ? <Loading rows={2} /> : shown.length === 0 ? <p className="px-3 py-2 text-sm text-muted-foreground">{t('decision.noItems')}</p> : shown.map((o) => {
          const on = selected?.includes(o.targetId) ?? false
          return (
            <button key={o.targetId} type="button" role="option" aria-selected={on} aria-label={`${t(`itemType.${o.targetType}`)} ${o.key} ${o.name}`} onClick={() => onPick(o)} className={cn('flex min-h-(--control-row-h) w-full items-center gap-2 px-3 py-1 text-left text-sm hover:bg-muted', on && 'bg-accent')}>
              {selected && <Check className={cn('size-4 shrink-0', !on && 'invisible')} aria-hidden />}
              <span className="w-24 shrink-0 text-xs/[18px] text-muted-foreground">{t(`itemType.${o.targetType}`)}</span><Key>{o.key}</Key><span className="min-w-0 flex-1 truncate">{o.name}</span>
            </button>
          )
        })}
      </div>
    </div>
  )
}

// ---------- Record, defer, cancel, reopen (DEC-02, DEC-03, DEC-07, FR-011) ----------

type Step = { to: string; ok: boolean; reason?: string | null }

/** One dialog with the decision text, its date and "notify linked task assignees", on by default (§13.8). */
export function RecordDecisionDialog({ id, onClose }: { id: string; onClose: (ok: boolean) => void }) {
  const q = useDecision(id)
  const done = useDecisionRefresh()
  const [text, setText] = useState('')
  const [date, setDate] = useState(today())
  const [notify, setNotify] = useState(true)
  const [err, setErr] = useState<ApiError | null>(null)
  const [busy, setBusy] = useState(false)
  const d = q.data?.decision
  const step = q.data?.permissions.transitions.find((x) => x.to === 'Decided')
  const tasks = q.data?.links.filter((l) => l.targetType === 'Task').length ?? 0
  const save = async () => {
    if (!d) return
    setErr(null); setBusy(true)
    try {
      await post(`decisions/${id}/transition`, { toStatus: 'Decided', decisionText: text, decisionDate: date, notifyAssignees: notify }, d.rowVersion)
      toast.success(t('decision.recorded', { key: d.key })); done(d.projectId, id); onClose(true)
    } catch (e) { setErr(e as ApiError) } finally { setBusy(false) }
  }
  return (
    <Dialog open onOpenChange={(o) => !o && onClose(false)}>
      <DialogContent>
        <DialogHeader><DialogTitle>{d ? t('decision.recordTitle', { key: d.key }) : t('decision.record')}</DialogTitle>{d && <DialogDescription>{d.subject}</DialogDescription>}</DialogHeader>
        {q.isPending ? <Loading rows={2} /> : q.error ? <ErrorBanner error={q.error} /> : step && !step.ok ? <p className="text-sm text-muted-foreground">{step.reason}</p> : (
          <div className="space-y-3">
            <Field label={t('decision.text')} htmlFor="rec-text" error={err?.fieldErrors.decisionText}><Textarea id="rec-text" autoFocus rows={4} value={text} onChange={(e) => setText(e.target.value)} /></Field>
            <Field label={t('decision.date')} htmlFor="rec-date" error={err?.fieldErrors.decisionDate}><Input id="rec-date" type="date" max={today()} value={date} onChange={(e) => setDate(e.target.value)} /></Field>
            <label className="flex items-center gap-2 text-sm"><Checkbox checked={notify} onCheckedChange={(c) => setNotify(!!c)} />{plural(tasks, 'decision.notifyAssignees1', 'decision.notifyAssignees')}</label>
            {err && !Object.keys(err.fieldErrors).length && <ErrorBanner error={err} />}
          </div>
        )}
        <DialogFooter>
          <Button variant="outline" onClick={() => onClose(false)}>{t('common.cancel')}</Button>
          <Button disabled={!text.trim() || busy || !step?.ok} onClick={save}>{busy && <Spinner />}{busy ? t('common.saving') : t('decision.record')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

function DeferDialog({ d, onClose }: { d: DecisionRow; onClose: () => void }) {
  const done = useDecisionRefresh()
  const [date, setDate] = useState(addDays(today(), 7))
  return (
    <ConfirmDialog open onOpenChange={(o) => !o && onClose()} reason title={t('decision.deferTitle', { key: d.key })} body={t('decision.deferHint', { date: fmtDate(d.requiredByDate) })}
      confirmLabel={t('decision.defer')} busy={!date || date <= today()}
      onConfirm={async (reason) => { await post(`decisions/${d.id}/transition`, { toStatus: 'Deferred', newRequiredBy: date, reason }, d.rowVersion); done(d.projectId, d.id) }}>
      <Field label={t('decision.newRequiredBy')} htmlFor="def-date" hint={t('decision.laterThanToday')}><Input id="def-date" type="date" min={addDays(today(), 1)} value={date} onChange={(e) => setDate(e.target.value)} /></Field>
    </ConfirmDialog>
  )
}

function StepDialogs({ d, step, onClose }: { d: DecisionRow; step: string; onClose: () => void }) {
  const done = useDecisionRefresh()
  const go = (body: object) => post(`decisions/${d.id}/transition`, body, d.rowVersion).then(() => done(d.projectId, d.id))
  if (step === 'Decided') return <RecordDecisionDialog id={d.id} onClose={onClose} />
  if (step === 'Deferred') return <DeferDialog d={d} onClose={onClose} />
  if (step === 'Cancelled') return <ConfirmDialog open onOpenChange={(o) => !o && onClose()} destructive reason title={t('decision.cancelTitle', { key: d.key })}
    body={t('decision.cancelHint')} confirmLabel={t('decision.cancel')} onConfirm={(reason) => go({ toStatus: 'Cancelled', reason })} />
  if (step === 'Pending' && d.status === 'Decided') return <ConfirmDialog open onOpenChange={(o) => !o && onClose()} reason title={t('decision.reopenTitle', { key: d.key })}
    body={t('decision.reopenHint')} confirmLabel={t('decision.reopen')} onConfirm={(reason) => go({ toStatus: 'Pending', reason })} />
  return null
}

const STEP_LABEL: Record<string, string> = { Decided: 'decision.record', Deferred: 'decision.defer', Cancelled: 'decision.cancel', 'Under Review': 'decision.toReview', Pending: 'decision.toPending' }

// ---------- Decision panel ----------

function DecisionPanel({ id }: PanelProps) {
  const q = useDecision(id)
  const done = useDecisionRefresh()
  const parties = useParties(q.data?.project.id ?? '')
  const [tab, setTab] = useState<'links' | 'comments' | 'history'>('links')
  const [step, setStep] = useState<string | null>(null)
  const [linking, setLinking] = useState(false)
  if (q.isPending) return <Loading />
  if (q.error) return <div className="p-4"><ErrorBanner error={q.error} /></div>
  const { decision: d, permissions: perm } = q.data
  const can = perm.edit.ok
  const isOpen = ['Pending', 'Under Review', 'Deferred'].includes(d.status)
  const save = (body: object) => patch(`decisions/${d.id}`, body, d.rowVersion).then(() => { done(d.projectId, d.id); return true }).catch((e) => { toast.error(errorText(e)); return false })
  const quick = async (s: Step) => {
    if (['Decided', 'Deferred', 'Cancelled'].includes(s.to) || (s.to === 'Pending' && d.status === 'Decided')) { setStep(s.to); return }
    try { await post(`decisions/${d.id}/transition`, { toStatus: s.to }, d.rowVersion); done(d.projectId, d.id) } catch (e) { toast.error(errorText(e)) }
  }
  const partyOptions = (parties.data ?? []).filter((x) => x.isActive || x.id === d.ownerExternalPartyId).map((x) => ({ value: x.id, label: `${x.name}${x.organisation ? ` · ${x.organisation}` : ''}` }))
  return (
    <div>
      <PanelHead title={d.subject} meta={<>
        <Scale className="size-4 text-muted-foreground" aria-hidden /><Key>{d.key}</Key><StatusPill status={d.status} /><Impact level={d.impactLevel} />
        {d.isOverdue && <Chip tone="bad">{t('ind.overdueD', { n: d.daysOverdue })}</Chip>}
        {d.isDueSoon && <Chip tone="warn">{t('decision.dueSoon')}</Chip>}
        {d.isInactiveOwner && <Chip tone="bad">{t('decision.inactiveOwner')}</Chip>}
      </>}>
        {d.blockingTaskIds.length > 0 && <p className="text-sm text-bad"><span aria-hidden>■ </span>{plural(d.blockingTaskIds.length, 'decision.holdingUp1', 'decision.holdingUp')} <Link className="underline underline-offset-4" to={`/projects/${q.data.project.projectNumber}/tasks?ids=${d.blockingTaskIds.join(',')}`}>{t('decision.openTasks')}</Link></p>}
        <div className="flex flex-wrap gap-2">
          {perm.transitions.map((s) => (
            <Button key={s.to} size="sm" variant={s.to === 'Decided' ? 'default' : 'outline'} disabled={!s.ok} title={s.reason ?? undefined} onClick={() => quick(s)}>
              {t(s.to === 'Pending' && d.status === 'Decided' ? 'decision.reopen' : STEP_LABEL[s.to])}
            </Button>
          ))}
        </div>
      </PanelHead>
      {d.status === 'Decided' && (
        <div className="mx-4 mb-4 rounded-md border border-done/30 bg-done-bg px-4 py-3 text-sm">
          <div className="text-xs/[18px] text-muted-foreground">{t('decision.decidedBy', { who: q.data.decidedBy ?? '', date: fmtDate(d.decisionDate) })}</div>
          <p className="mt-1 whitespace-pre-wrap">{d.decisionText}</p>
        </div>
      )}
      {d.status === 'Deferred' && q.data.deferralReason && <p className="mx-4 mb-4 rounded-md bg-warn-bg px-4 py-3 text-sm">{t('decision.deferredNote', { reason: q.data.deferralReason })}</p>}
      {d.status === 'Cancelled' && q.data.cancelledReason && <p className="mx-4 mb-4 rounded-md bg-idle-bg px-4 py-3 text-sm">{t('decision.cancelledNote', { reason: q.data.cancelledReason })}</p>}
      <FieldGroup title={t('common.details')}>
        <FieldRow label={t('decision.subject')}><InlineText value={d.subject} disabled={!can} title={perm.edit.reason ?? undefined} onSave={(v) => save({ subject: v })} /></FieldRow>
        <FieldRow label={t('common.description')}><InlineText value={q.data.description} multiline disabled={!can} onSave={(v) => save({ description: v })} /></FieldRow>
        <FieldRow label={t('decision.impact')}><InlineSelect value={d.impactLevel} options={IMPACTS.map((x) => ({ value: x, label: tv(x) }))} disabled={!can} onSave={(v) => save({ impactLevel: v })} title={t('decision.impact')} /></FieldRow>
        <FieldRow label={t('decision.impactDescription')}><InlineText value={d.impactDescription} disabled={!can} onSave={(v) => save({ impactDescription: v })} /></FieldRow>
      </FieldGroup>
      <FieldGroup title={t('register.group.ownership')}>
        <FieldRow label={t('common.owner')}>
          {!can ? <div className="px-2 py-1.5"><Owner d={d} /></div> : d.ownerExternalPartyId
            ? <InlineSelect value={d.ownerExternalPartyId} options={partyOptions} onSave={(v) => save({ ownerExternalPartyId: v })} title={t('decision.owner.party')} />
            : <PeoplePicker compact value={d.ownerUserId} valueName={d.ownerName} allowClear={false} onChange={(v) => v && save({ ownerUserId: v })} label={t('decision.owner.person')} />}
          {can && <OwnerSwitch d={d} parties={partyOptions} onSave={save} />}
        </FieldRow>
        <FieldRow label={t('decision.requestedBy')}>{can
          ? <PeoplePicker compact value={d.requestedById} valueName={d.requestedByName} allowClear={false} onChange={(v) => v && save({ requestedById: v })} label={t('decision.requestedBy')} />
          : <div className="px-2 py-1.5"><Person id={d.requestedById} name={d.requestedByName} /></div>}</FieldRow>
      </FieldGroup>
      <FieldGroup title={t('allocation.dates')}>
        <FieldRow label={t('decision.requested')}><InlineDate value={d.dateRequested} disabled={!can} onSave={(v) => save({ dateRequested: v })} /></FieldRow>
        <FieldRow label={t('decision.requiredBy')}><InlineDate value={d.requiredByDate} disabled={!can || !isOpen} title={isOpen ? undefined : t('decision.dateClosed')} onSave={(v) => save({ requiredByDate: v })} /></FieldRow>
        {d.originalRequiredByDate !== d.requiredByDate && <FieldRow label={t('decision.originalRequiredBy')}><div className="px-2 py-1.5 text-muted-foreground tabular-nums">{fmtDate(d.originalRequiredByDate)}</div></FieldRow>}
      </FieldGroup>
      <TabBar tabs={[{ id: 'links' as const, label: t('decision.links'), count: q.data.links.length }, ...(ItemSlots.Comments ? [{ id: 'comments' as const, label: t('common.comments') }] : []), { id: 'history' as const, label: t('common.history') }]} value={tab} onChange={setTab} />
      {tab === 'links' && (
        <div className="space-y-3 p-4">
          <LinkedItems id={d.id} canRemove={can} />
          {can && (linking ? <AddLinkBox d={d} exclude={q.data.links.map((l) => l.targetId)} onDone={() => setLinking(false)} />
            : <Button size="sm" variant="outline" onClick={() => setLinking(true)}><Plus className="size-4" />{t('decision.addLink')}</Button>)}
        </div>
      )}
      {tab === 'comments' && <CommentsSlot type="Decision" id={d.id} projectId={d.projectId} />}
      {tab === 'history' && <HistoryList type="Decision" id={d.id} />}
      {step && <StepDialogs d={d} step={step} onClose={() => setStep(null)} />}
    </div>
  )
}

/** Switch between an internal and an external owner; a switch needs the new owner chosen in the same step. */
function OwnerSwitch({ d, parties, onSave }: { d: DecisionRow; parties: { value: string; label: string }[]; onSave: (b: object) => Promise<boolean> }) {
  const [open, setOpen] = useState(false)
  const [choice, setChoice] = useState<string | null>(null)
  const toParty = !d.ownerExternalPartyId
  if (!open) return <button type="button" className="px-2 text-sm text-primary underline underline-offset-4 hover:text-foreground" onClick={() => setOpen(true)}>{t(toParty ? 'decision.switchToParty' : 'decision.switchToPerson')}</button>
  return (
    <div className="mt-1 flex items-center gap-2 px-2">
      {toParty
        ? <select className={cn(selectCls, 'h-(--control-row-h)')} value={choice ?? ''} onChange={(e) => setChoice(e.target.value || null)} aria-label={t('decision.owner.party')}>
            <option value="">{t('decision.chooseParty')}</option>{parties.map((x) => <option key={x.value} value={x.value}>{x.label}</option>)}
          </select>
        : <PeoplePicker value={choice} onChange={(v) => setChoice(v)} label={t('decision.owner.person')} />}
      <Button size="sm" disabled={!choice} onClick={() => onSave(toParty ? { ownerExternalPartyId: choice } : { ownerUserId: choice }).then((ok) => ok && setOpen(false))}>{t('common.save')}</Button>
      <Button size="sm" variant="ghost" onClick={() => setOpen(false)}>{t('common.cancel')}</Button>
    </div>
  )
}

interface BulkResult { linked: { id: string; key: string }[]; skipped: { id: string; key?: string | null; reason: string }[] }

/** Pick many items and link them in one action (FR-002); items the user may not change are reported, not linked. */
function AddLinkBox({ d, exclude, onDone }: { d: DecisionRow; exclude: string[]; onDone: () => void }) {
  const done = useDecisionRefresh()
  const [related, setRelated] = useState(false)
  const [picked, setPicked] = useState<Pick[]>([])
  const [skipped, setSkipped] = useState<BulkResult['skipped']>([])
  const [busy, setBusy] = useState(false)
  const has = (list: Pick[], x: Pick) => list.some((y) => y.targetId === x.targetId)
  const link = async () => {
    setBusy(true); setSkipped([])
    try {
      const out: BulkResult = { linked: [], skipped: [] }
      for (const type of ['Task', 'Deliverable', 'Milestone'] as const) {
        const ids = picked.filter((x) => x.targetType === type).map((x) => x.targetId)
        if (!ids.length) continue
        const r = await post<BulkResult>(`decisions/${d.id}/links/bulk`, { targetType: type, targetIds: ids, relation: type === 'Task' && !related ? 'blocked_by_decision' : 'related' })
        out.linked.push(...r.linked); out.skipped.push(...r.skipped)
      }
      done(d.projectId, d.id)
      if (out.linked.length) toast.success(plural(out.linked.length, 'decision.linked1', 'decision.linkedN'))
      if (out.skipped.length) { setSkipped(out.skipped); setPicked([]) } else onDone()
    } catch (e) { toast.error(errorText(e)) } finally { setBusy(false) }
  }
  return (
    <div className="space-y-3 rounded-lg border bg-muted p-3">
      <label className="flex items-center gap-2 text-sm"><Checkbox checked={related} onCheckedChange={(c) => setRelated(!!c)} />{t('decision.tasksRelated')}</label>
      <ItemPicker projectId={d.projectId} exclude={exclude} selected={picked.map((x) => x.targetId)}
        onPick={(x) => setPicked((cur) => (has(cur, x) ? cur.filter((y) => y.targetId !== x.targetId) : [...cur, x]))}
        onPickAll={(xs) => setPicked((cur) => [...cur, ...xs.filter((x) => !has(cur, x))])} />
      {skipped.length > 0 && (
        <div role="status" className="rounded-md border border-warn/40 bg-warn-bg px-3 py-2 text-sm">
          <p className="font-medium text-warn"><span aria-hidden>▲ </span>{plural(skipped.length, 'decision.skipped1', 'decision.skippedN')}</p>
          <ul className="mt-1 space-y-0.5">{skipped.map((x) => <li key={x.id}>{x.key && <Key>{x.key}</Key>} {x.reason}</li>)}</ul>
        </div>
      )}
      <div className="flex items-center justify-end gap-2">
        {picked.length > 0 && <Button type="button" variant="link" size="sm" className="mr-auto px-0" onClick={() => setPicked([])}>{t('common.clear')}</Button>}
        <Button size="sm" variant="ghost" onClick={onDone}>{t('common.close')}</Button>
        <Button size="sm" disabled={!picked.length || busy} onClick={link}>{busy && <Spinner />}{busy ? t('common.saving') : picked.length ? plural(picked.length, 'decision.linkN1', 'decision.linkN') : t('decision.linkItems')}</Button>
      </div>
    </div>
  )
}

// ---------- External parties (FR-ORG-08, FR-009) ----------

function PartyForm({ p, party, onClose }: { p: ProjectDetail; party?: Party; onClose: (created?: { id: string }) => void }) {
  const qc = useQueryClient()
  const [f, setF] = useState<Record<string, any>>(party ? { ...party } : { isClient: false })
  const [err, setErr] = useState<ApiError | null>(null)
  const submit = async () => {
    setErr(null)
    try {
      const body = { name: f.name, organisation: f.organisation || null, email: f.email || null, role: f.role || null, isClient: !!f.isClient, notes: f.notes || null, ...(party ? { isActive: f.isActive } : {}) }
      const r = party ? await patch(`external-parties/${party.id}`, body, party.rowVersion) : await post(`projects/${p.id}/external-parties`, body)
      qc.invalidateQueries({ queryKey: ['p', p.id, 'parties'] })
      toast.success(t('common.saved')); onClose(party ? undefined : r)
    } catch (e) { setErr(e as ApiError) }
  }
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent>
        <DialogHeader><DialogTitle>{party ? t('party.edit', { name: party.name }) : t('party.new')}</DialogTitle><DialogDescription>{t('party.hint')}</DialogDescription></DialogHeader>
        <form className="grid gap-3 sm:grid-cols-2" onSubmit={(e) => { e.preventDefault(); e.stopPropagation(); submit() }}>
          <Field label={t('common.name')} htmlFor="ep-name" error={err?.fieldErrors.name}><Input id="ep-name" required value={f.name ?? ''} onChange={(e) => setF({ ...f, name: e.target.value })} /></Field>
          <Field label={t('party.organisation')} htmlFor="ep-org"><Input id="ep-org" value={f.organisation ?? ''} onChange={(e) => setF({ ...f, organisation: e.target.value })} /></Field>
          <Field label={t('party.email')} htmlFor="ep-email" hint={t('party.emailHint')}><Input id="ep-email" type="email" value={f.email ?? ''} onChange={(e) => setF({ ...f, email: e.target.value })} /></Field>
          <Field label={t('party.role')} htmlFor="ep-role"><Input id="ep-role" value={f.role ?? ''} onChange={(e) => setF({ ...f, role: e.target.value })} /></Field>
          <label className="flex items-center gap-2 text-sm"><Checkbox checked={!!f.isClient} onCheckedChange={(c) => setF({ ...f, isClient: !!c })} />{t('party.isClient')}</label>
          {party && <label className="flex items-center gap-2 text-sm"><Checkbox checked={!!f.isActive} onCheckedChange={(c) => setF({ ...f, isActive: !!c })} />{t('party.active')}</label>}
          <Field label={t('common.notes')} htmlFor="ep-notes" className="sm:col-span-2"><Textarea id="ep-notes" rows={2} value={f.notes ?? ''} onChange={(e) => setF({ ...f, notes: e.target.value })} /></Field>
          {err && !Object.keys(err.fieldErrors).length && <div className="sm:col-span-2"><ErrorBanner error={err} /></div>}
          <DialogFooter className="sm:col-span-2"><Button type="button" variant="outline" onClick={() => onClose()}>{t('common.cancel')}</Button><Button type="submit">{t('common.save')}</Button></DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

function PartiesDialog({ p, onClose }: { p: ProjectDetail; onClose: () => void }) {
  const q = useParties(p.id)
  const [form, setForm] = useState<Party | 'new' | null>(null)
  const canEdit = p.permissions.isPm && p.permissions.edit.ok
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="sm:max-w-2xl">
        <DialogHeader><DialogTitle>{t('party.title')}</DialogTitle><DialogDescription>{t('party.listHint')}</DialogDescription></DialogHeader>
        {q.isPending ? <Loading rows={3} /> : !q.data?.length ? <Empty>{t('party.empty')}</Empty> : (
          <div className="scroll-region max-h-80 overflow-y-auto rounded-md border">
            <table className="w-full text-sm">
              <thead className="sticky top-0 bg-muted text-left text-muted-foreground"><tr>
                {['common.name', 'party.organisation', 'party.role', 'party.email'].map((h) => <th key={h} scope="col" className="px-3 py-2 font-medium">{t(h)}</th>)}
                <th scope="col"><span className="sr-only">{t('common.actions')}</span></th>
              </tr></thead>
              <tbody>{q.data.map((x) => (
                <tr key={x.id} className={cn('border-t', !x.isActive && 'text-muted-foreground')}>
                  <td className="px-3 py-2">{x.name}{x.isClient && <span data-accent="lavender" className="ml-2 rounded-md bg-(--acc-bg) px-1.5 py-0.5 text-xs font-medium text-(--acc-fg)">{t('party.client')}</span>}{!x.isActive && ` (${t('party.inactive')})`}</td>
                  <td className="px-3 py-2">{x.organisation}</td><td className="px-3 py-2">{x.role}</td><td className="px-3 py-2">{x.email}</td>
                  <td className="px-2 py-1 text-right">{canEdit && <Button size="sm" variant="ghost" onClick={() => setForm(x)}>{t('common.edit')}</Button>}</td>
                </tr>))}</tbody>
            </table>
          </div>
        )}
        <DialogFooter>
          {p.permissions.raiseRegister.ok && <Button variant="outline" onClick={() => setForm('new')}><Plus className="size-4" />{t('party.new')}</Button>}
          <Button onClick={onClose}>{t('common.close')}</Button>
        </DialogFooter>
        {form && <PartyForm p={p} party={form === 'new' ? undefined : form} onClose={() => setForm(null)} />}
      </DialogContent>
    </Dialog>
  )
}

PANELS.Decision = { component: DecisionPanel }
