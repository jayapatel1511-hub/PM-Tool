import { disciplineColour } from '@/lib/discipline-colour'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ChevronDown, ChevronRight, ChevronsDownUp, ChevronsUpDown, ListTree, Monitor, Printer, ZoomIn } from 'lucide-react'
import { Fragment, useRef, useState, type PointerEvent as ReactPointerEvent, type ReactNode } from 'react'
import { useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { ActiveFilters, ChoiceMenu, ConfirmDialog, DesktopOnly, Empty, ErrorBanner, Field, FilterBar, Loading, Notice, Page, Section, selectCls, useIsPhone } from '@/components/hub/common'
import { useItemPanel } from '@/components/hub/panel-host'
import { Key, toneOf } from '@/components/hub/pills'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { useProjectRefresh } from '@/hooks/data'
import { get, patch } from '@/lib/api'
import { addDays, daysBetween, fmtDate, monthLabel, shortDate, today } from '@/lib/format'
import { plural, t, tv } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import type { DeliverableRow } from './Deliverables'
import { ChangeDateDialog, milestoneStatusLabel, type MilestoneRow } from './Milestones'
import { useCurrentProject } from './ProjectLayout'

/** Pixels per day at each zoom (§12.16: week and month; quarter for long projects). */
export const ZOOM = { week: 28, month: 7, quarter: 2.4 } as const
export type Zoom = keyof typeof ZOOM
/** Zoom as a compact toolbar menu (§41 R1). */
export const ZoomMenu = ({ value, onChange }: { value: Zoom; onChange: (z: Zoom) => void }) => (
  <ChoiceMenu label={t('timeline.zoom')} icon={ZoomIn} value={value} onChange={onChange} options={(Object.keys(ZOOM) as Zoom[]).map((z) => ({ value: z, label: t(`timeline.zoom.${z}`) }))} />
)
/** A bar's first segment, from its assumed start to today (at least its first day), is drawn faded (§40 AC-TL-01). */
export const assumedWidth = (span: { start: string; end: string; assumed?: boolean }, now: string, px: number) =>
  span.assumed ? Math.max(daysBetween(span.start, now < span.end ? now : addDays(span.end, 1)), 1) * px : 0
/** Arrow ends by link type (§40 AC-DEPT-06): finish → start, start → start or finish → finish. `s` and `e` are the bar's
 *  left and right edges in px. */
export function depPath(type: string | undefined, a: { s: number; e: number; y: number }, b: { s: number; e: number; y: number }) {
  const [x1, out, x2, into] = type === 'StartToStart' ? [a.s, -1, b.s, 1] : type === 'FinishToFinish' ? [a.e, 1, b.e, -1] : [a.e, 1, b.s, 1]
  const bend = Math.max(12, Math.abs(x2 - x1) / 3)
  return `M ${x1} ${a.y} C ${x1 + out * bend} ${a.y}, ${x2 - into * bend} ${b.y}, ${x2 - into * 2} ${b.y}`
}
/** An arrow's tooltip: its link type and lag (left out when the link has no lag of its own, as for a deliverable link
 *  derived from task links). */
export const depTitle = (type: string | undefined, lag?: number) =>
  [t(`sig.dep.${type ?? 'FinishToStart'}`), lag === undefined ? null : lag ? plural(lag, 'sig.depLag1', 'sig.depLag') : t('sig.depNoLag')].filter(Boolean).join(' · ')
/** One dependency arrow with a hover target and a tooltip naming its type and lag. */
export function DepArrow({ d, hot, title, marker }: { d: string; hot: boolean; title: string; marker: string }) {
  return (
    <g>
      <title>{title}</title>
      <path d={d} fill="none" stroke="transparent" strokeWidth={6} style={{ pointerEvents: 'stroke' }} />
      <path d={d} fill="none" strokeWidth={hot ? 1.6 : 1} stroke={hot ? 'var(--bad)' : 'var(--muted-foreground)'} strokeOpacity={hot ? 1 : 0.55} markerEnd={`url(#${marker}-${hot ? 'hot' : 'cold'})`} />
    </g>
  )
}
const LABEL = 224
const H = { group: 28, deliverable: 32, loose: 24, task: 24 } as const
const CLOSED = ['Issued', 'Accepted', 'Cancelled']
const STATUSES = ['Not Started', 'In Progress', 'In Review', 'Revision Required', 'Ready to Issue', 'Issued', 'Accepted', 'On Hold']
export const tone = (status?: string | null) => `var(--${toneOf(status)})`
export const toneBg = (status?: string | null) => `var(--${toneOf(status)}-bg)`
const min = (xs: (string | null | undefined)[]) => xs.filter(Boolean).reduce<string | undefined>((a, b) => (!a || b! < a ? b! : a), undefined)
const max = (xs: (string | null | undefined)[]) => xs.filter(Boolean).reduce<string | undefined>((a, b) => (!a || b! > a ? b! : a), undefined)
export const HATCH = 'repeating-linear-gradient(135deg, var(--bad) 0 3px, transparent 3px 7px)'
const FILTERS = ['disciplineId', 'milestoneId', 'status', 'hideCompleted', 'from', 'to'] as const

interface TimelineTask {
  id: string; key: string; name: string; deliverableId?: string | null; projectDisciplineId: string; milestoneId?: string | null; startDate?: string | null; dueDate?: string | null
  originalStartDate?: string | null; originalDueDate?: string | null; status: string; progressPct: number; assignee?: string | null; rowVersion: number; createdOn: string
  isOverdue: boolean; canEditDates: boolean; dueNeedsReason: boolean
}
interface TimelineData {
  permissions: { manageMilestones: boolean; needsReason: boolean }
  tasks: TimelineTask[]
  dependencies: { id: string; predecessorTaskId: string; successorTaskId: string; type: string; lagDays: number; highlighted: boolean }[]
  deliverableLinks: { from: string; to: string; type: string; lagDays?: number | null; highlighted: boolean; derived: boolean }[]
  deliverables: { id: string; canEditDates: boolean; createdOn: string }[]
}
/** `assumed`: no start date, so the bar starts at the creation date (§40 AC-TL-01). */
interface Span { start: string; end: string; late: boolean; assumed?: boolean; ghost?: { start: string; end: string } | null }
type RowT =
  | { kind: 'group'; id: string; name: string; colour?: string; count: number }
  | { kind: 'deliverable'; id: string; d: DeliverableRow; span: Span; tasks: number }
  | { kind: 'loose'; id: string; count: number }
  | { kind: 'task'; id: string; tk: TimelineTask; span: Span }
export type Move = { kind: 'task' | 'deliverable'; id: string; key: string; name: string; start?: string | null; due: string; days: number; rowVersion: number; needsReason: boolean }

/** Timeline (§12.16, §13.5, §36.4, FR-VIEW-01/02): milestones on a top lane, deliverables by discipline with their tasks,
 *  dependency arrows (red while the predecessor is unfinished and overdue), ghost bars at original dates, and dragging
 *  to a new date with a confirmation. Only the confirmed item's dates change; nothing is rescheduled automatically. */
export function TimelineTab() {
  const p = useCurrentProject()
  const [sp, setSp] = useSearchParams()
  const openPanel = useItemPanel()
  const refresh = useProjectRefresh()
  const qc = useQueryClient()
  const [closed, setClosed] = useState<Set<string>>(new Set())
  const [expanded, setExpanded] = useState<Set<string>>(new Set())
  const [shift, setShift] = useState<{ id: string; days: number } | null>(null)
  const [move, setMove] = useState<Move | null>(null)
  const [msMove, setMsMove] = useState<{ m: MilestoneRow; date: string } | null>(null)
  const drag = useRef<{ id: string; x0: number; moved: boolean } | null>(null)
  const suppress = useRef(false)
  const phone = useIsPhone() // milestone editing is desktop/tablet only (§13.0); on phones the diamonds open the milestone instead
  const set = (k: string, v?: string | null) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); n.delete('panel'); setSp(n, { replace: true }) }
  const zoom = (sp.get('zoom') as Zoom | null) ?? 'month'
  const f = { disciplineId: sp.get('disciplineId'), milestoneId: sp.get('milestoneId'), status: sp.get('status'), hide: sp.get('hideCompleted') === 'true', from: sp.get('from'), to: sp.get('to') }
  const ms = useQuery({ queryKey: ['p', p.id, 'milestones', true, ''], queryFn: () => get<MilestoneRow[]>(`projects/${p.id}/milestones?showCompleted=true`) })
  const dels = useQuery({ queryKey: ['p', p.id, 'deliverables', {}], queryFn: () => get<DeliverableRow[]>(`projects/${p.id}/deliverables`) })
  const tl = useQuery({ queryKey: ['p', p.id, 'timeline'], queryFn: () => get<TimelineData>(`projects/${p.id}/timeline`) })
  const header = { title: t('ptab.timeline'), subtitle: t('calm.timeline.scope') }
  if (ms.isPending || dels.isPending || tl.isPending) return <Page {...header}><div className="rounded-lg border bg-card"><Loading rows={8} /></div></Page>
  if (ms.error || dels.error || tl.error) return <Page {...header}><ErrorBanner error={ms.error ?? dels.error ?? tl.error} retry={() => { ms.refetch(); dels.refetch(); tl.refetch() }} /></Page>

  const now = today()
  const px = ZOOM[zoom]
  const data = tl.data
  const canDel = new Map(data.deliverables.map((x) => [x.id, x.canEditDates]))
  const inRange = (a?: string | null, b?: string | null) => (!f.from || (b ?? a ?? '') >= f.from) && (!f.to || (a ?? b ?? '') <= f.to)
  const ghostOf = (os?: string | null, od?: string | null, s?: string | null, d?: string | null) => (od && (od !== d || (os ?? null) !== (s ?? null)) ? { start: os ?? od, end: od } : null)
  const msShown = ms.data.filter((m) => !m.isCancelled && (!f.hide || !m.isComplete) && (!f.milestoneId || m.id === f.milestoneId))
  const milestones = msShown.filter((m) => m.date && inRange(m.date, m.date))
  const delById = new Map(dels.data.map((d) => [d.id, d]))
  const items = dels.data.filter((d) => (!f.disciplineId || d.projectDisciplineId === f.disciplineId) && (!f.milestoneId || d.milestoneId === f.milestoneId)
    && (!f.status || d.status === f.status) && (!f.hide || !CLOSED.includes(d.status)))
  const spanOf = (start: string | null | undefined, created: string, due: string, overdue: boolean) => { const b = start ?? created; return { start: b > due ? due : b, end: due, late: overdue && due < now, assumed: !start } }
  // AC-TL-04: the creation date is the server's local date, not a cut UTC timestamp.
  const createdOn = new Map(data.deliverables.map((x) => [x.id, x.createdOn]))
  const delSpans = new Map(items.filter((d) => d.dueDate).map((d) => [d.id, { ...spanOf(d.startDate, createdOn.get(d.id) ?? d.dueDate!, d.dueDate!, !!d.state?.isOverdue),
    ghost: ghostOf(d.originalStartDate, d.originalDueDate, d.startDate, d.dueDate) } as Span]))
  const visibleDel = new Set([...delSpans.keys()].filter((id) => { const s = delSpans.get(id)!; return inRange(s.start, s.late ? now : s.end) }))
  // A task follows its deliverable through the filters; one under a deliverable without a bar here (undated or outside the
  // date range) is drawn as a top-level row instead of being dropped (AC-TL-02).
  const shownDel = new Set(items.map((d) => d.id))
  const tasks = data.tasks.filter((tk) => (!f.hide || tk.status !== 'Complete') && (!f.disciplineId || tk.projectDisciplineId === f.disciplineId)
    && (!f.milestoneId || (tk.milestoneId ?? (tk.deliverableId ? delById.get(tk.deliverableId)?.milestoneId : null)) === f.milestoneId)
    && (tk.deliverableId && delById.has(tk.deliverableId) ? shownDel.has(tk.deliverableId) : !f.status))
  const taskSpan = (tk: TimelineTask): Span | null => tk.dueDate ? { ...spanOf(tk.startDate, tk.createdOn, tk.dueDate, tk.isOverdue), ghost: ghostOf(tk.originalStartDate, tk.originalDueDate, tk.startDate, tk.dueDate) } : null
  const nested = (tk: TimelineTask) => !!tk.deliverableId && visibleDel.has(tk.deliverableId)
  const loose = tasks.filter((tk) => { const sp = taskSpan(tk); return !nested(tk) && sp && inRange(sp.start, sp.late ? now : sp.end) })
  const unscheduled = [...items.filter((d) => !d.dueDate).map((d) => ({ type: 'Deliverable' as const, id: d.id, key: d.key, name: d.name, sub: d.disciplineName })),
    ...tasks.filter((tk) => !tk.dueDate).map((tk) => ({ type: 'Task' as const, id: tk.id, key: tk.key, name: tk.name, sub: tk.assignee })),
    ...msShown.filter((m) => !m.date).map((m) => ({ type: 'Milestone' as const, id: m.id, key: m.key, name: m.name, sub: undefined }))] // AC-TL-03

  // Rows in order: discipline, its deliverables (each followed by its tasks when expanded), then the tasks on their own.
  const order = new Map(p.disciplines.map((d, i) => [d.id, i]))
  const pds = [...new Set([...[...visibleDel].map((id) => delById.get(id)!.projectDisciplineId), ...loose.map((tk) => tk.projectDisciplineId)])]
    .sort((a, b) => (order.get(a) ?? 99) - (order.get(b) ?? 99))
  const rows: RowT[] = []
  for (const pd of pds) {
    const disc = p.disciplines.find((d) => d.id === pd)
    const groupDels = [...visibleDel].map((id) => delById.get(id)!).filter((d) => d.projectDisciplineId === pd).sort((a, b) => a.dueDate!.localeCompare(b.dueDate!))
    const solo = loose.filter((tk) => tk.projectDisciplineId === pd)
    rows.push({ kind: 'group', id: pd, name: disc?.name ?? '', colour: disc?.colour, count: groupDels.length + solo.length })
    if (closed.has(pd)) continue
    for (const d of groupDels) {
      const own = tasks.filter((tk) => tk.deliverableId === d.id && tk.dueDate)
      rows.push({ kind: 'deliverable', id: d.id, d, span: delSpans.get(d.id)!, tasks: own.length })
      if (expanded.has(d.id)) for (const tk of own.sort((a, b) => (a.startDate ?? a.dueDate!).localeCompare(b.startDate ?? b.dueDate!))) rows.push({ kind: 'task', id: tk.id, tk, span: taskSpan(tk)! })
    }
    if (solo.length) {
      rows.push({ kind: 'loose', id: `loose:${pd}`, count: solo.length })
      if (expanded.has(`loose:${pd}`)) for (const tk of solo.sort((a, b) => a.dueDate!.localeCompare(b.dueDate!))) rows.push({ kind: 'task', id: tk.id, tk, span: taskSpan(tk)! })
    }
  }
  const spans = rows.flatMap((r) => (r.kind === 'deliverable' || r.kind === 'task' ? [r.span] : []))

  const start = addDays(f.from ?? min([p.startDate, addDays(now, -7), ...spans.flatMap((s) => [s.start, s.ghost?.start]), ...milestones.flatMap((m) => [m.date, m.originalDate])])!, -3)
  const end = addDays(f.to ?? max([p.targetCompletionDate, addDays(now, 14), ...spans.flatMap((s) => [s.late ? now : s.end, s.ghost?.end]), ...milestones.flatMap((m) => [m.date, m.originalDate])])!, 7)
  const days = daysBetween(start, end) + 1
  const width = Math.round(days * px)
  const x = (d: string) => daysBetween(start, d) * px
  const visible = (d?: string | null) => !!d && d >= start && d <= end

  const ticks: { date: string; label?: string; major: boolean }[] = []
  for (let i = 0; i < days; i++) {
    const d = addDays(start, i)
    const dow = new Date(d + 'T00:00:00Z').getUTCDay()
    const first = d.endsWith('-01')
    const quarter = ['01', '04', '07', '10'].includes(d.slice(5, 7))
    if (zoom === 'week') ticks.push({ date: d, label: dow === 1 ? shortDate(d) : undefined, major: dow === 1 })
    else if (zoom === 'month' && (dow === 1 || first)) ticks.push({ date: d, label: first ? monthLabel(d) : undefined, major: first })
    else if (zoom === 'quarter' && first) ticks.push({ date: d, label: quarter ? monthLabel(d) : shortDate(d).split(' ')[0], major: quarter })
  }

  const lanes: number[] = []
  const placed = [...milestones].sort((a, b) => a.date!.localeCompare(b.date!)).map((m) => {
    const left = x(m.date!)
    const w = Math.min(176, 44 + m.name.length * 7)
    let row = lanes.findIndex((e) => e < left - 6)
    if (row < 0) row = lanes.length < 3 ? lanes.length : lanes.indexOf(Math.min(...lanes))
    lanes[row] = left + w
    return { m, left, w, row }
  })
  const laneHeight = 24 + Math.max(lanes.length, 1) * 16 + 4

  // Row positions for the dependency arrows.
  const pos = new Map<string, { y: number; h: number }>()
  let y = 0
  for (const r of rows) { const h = H[r.kind]; pos.set(r.id, { y, h }); y += h }
  const bodyHeight = y
  const arrow = (fromId: string, fromSpan: Span, toId: string, toSpan: Span, hot: boolean, key: string, type: string, lag?: number) => {
    const a = pos.get(fromId), b = pos.get(toId)
    if (!a || !b) return null
    const d = depPath(type, { s: x(fromSpan.start), e: x(fromSpan.end) + px, y: a.y + a.h / 2 }, { s: x(toSpan.start), e: x(toSpan.end) + px, y: b.y + b.h / 2 })
    return <DepArrow key={key} d={d} hot={hot} title={depTitle(type, lag)} marker="arrow" />
  }
  const taskRows = new Map(rows.flatMap((r) => (r.kind === 'task' ? [[r.id, r] as const] : [])))
  const delRows = new Map(rows.flatMap((r) => (r.kind === 'deliverable' ? [[r.id, r] as const] : [])))
  const arrows = [
    ...data.dependencies.map((dep) => { const a = taskRows.get(dep.predecessorTaskId), b = taskRows.get(dep.successorTaskId); return a && b ? arrow(a.id, a.span, b.id, b.span, dep.highlighted, dep.id, dep.type, dep.lagDays) : null }),
    ...data.deliverableLinks.map((l) => { const a = delRows.get(l.from), b = delRows.get(l.to); return a && b && !expanded.has(a.id) && !expanded.has(b.id) ? arrow(a.id, a.span, b.id, b.span, l.highlighted, `${l.from}-${l.to}`, l.type, l.lagDays ?? undefined) : null }),
  ].filter(Boolean)

  const lines = [
    { date: now, label: t('common.today'), cls: 'border-primary border-l-2', text: 'text-primary', offset: px / 2 },
    ...(p.startDate ? [{ date: p.startDate, label: t('timeline.start'), cls: 'border-l border-dashed border-foreground/40', text: 'text-muted-foreground', offset: 0 }] : []),
    ...(p.targetCompletionDate ? [{ date: p.targetCompletionDate, label: t('timeline.target'), cls: 'border-l border-dashed border-foreground/40', text: 'text-muted-foreground', offset: 0 }] : []),
  ].filter((l) => visible(l.date))

  /** Pointer drag that snaps to days; a click without movement opens the item instead (FR-003). */
  const dragProps = (id: string, can: boolean, drop: (days: number) => void, open: () => void) => ({
    onPointerDown: (e: ReactPointerEvent<HTMLElement>) => { if (!can || e.button !== 0) return; e.currentTarget.setPointerCapture(e.pointerId); drag.current = { id, x0: e.clientX, moved: false } },
    onPointerMove: (e: ReactPointerEvent<HTMLElement>) => {
      const d = drag.current
      if (!d || d.id !== id) return
      const n = Math.round((e.clientX - d.x0) / px)
      if (n !== 0) d.moved = true
      setShift(n ? { id, days: n } : null)
    },
    onPointerUp: (e: ReactPointerEvent<HTMLElement>) => {
      const d = drag.current
      drag.current = null
      if (!d || d.id !== id) return
      const n = Math.round((e.clientX - d.x0) / px)
      setShift(null)
      if (d.moved) { suppress.current = true; if (n !== 0) drop(n) }
    },
    onClick: () => { if (suppress.current) { suppress.current = false; return } open() },
    style: { touchAction: can ? 'none' : undefined, cursor: can ? 'grab' : 'pointer' } as const,
  })
  const dx = (id: string) => (shift?.id === id ? shift.days * px : 0)
  const toggle = (setter: typeof setClosed, id: string) => setter((s) => { const n = new Set(s); if (n.has(id)) n.delete(id); else n.add(id); return n })
  const expandable = rows.flatMap((r) => (r.kind === 'deliverable' && r.tasks > 0 ? [r.id] : r.kind === 'loose' ? [r.id] : []))
  const allOpen = expandable.length > 0 && expandable.every((id) => expanded.has(id))
  const reload = () => { qc.invalidateQueries({ queryKey: ['p', p.id] }); refresh(p.id) }
  // Active filters as removable tokens with Clear (§13.0 Filters); zoom is a view choice, not a filter.
  const tokens = ([
    ['disciplineId', t('common.discipline'), p.disciplines.find((d) => d.id === f.disciplineId)?.name],
    ['milestoneId', t('field.MilestoneId'), ms.data.find((m) => m.id === f.milestoneId)?.key],
    ['status', t('common.status'), f.status && tv(f.status)],
    ['hideCompleted', t('timeline.hideCompleted'), t('common.yes')],
    ['from', t('common.from'), f.from && fmtDate(f.from)],
    ['to', t('common.to'), f.to && fmtDate(f.to)],
  ] as const).filter(([k]) => sp.get(k))
  const clear = () => { const n = new URLSearchParams(sp); for (const k of FILTERS) n.delete(k); n.delete('panel'); setSp(n, { replace: true }) }

  const bar = (id: string, span: Span, status: string, pct: number, text: string, thin: boolean, can: boolean, onDrop: (n: number) => void, open: () => void) => {
    const label = span.assumed ? `${text} · ${t('sig.startNotSet')}` : text
    const left = Math.max(x(span.start), 0)
    const w = Math.max((daysBetween(span.start, span.end) + 1) * px, 4)
    const top = thin ? 7 : 7
    const height = thin ? 10 : 18
    return (
      <>
        {span.ghost && visible(span.ghost.end) && (
          <span aria-hidden className="absolute rounded-sm border border-dashed border-foreground/40" title={t('timeline.baseline', { from: fmtDate(span.ghost.start), to: fmtDate(span.ghost.end) })}
            style={{ left: Math.max(x(span.ghost.start), 0), width: Math.max((daysBetween(span.ghost.start, span.ghost.end) + 1) * px, 4), top: top - 3, height: height + 6 }} />
        )}
        <button type="button" {...dragProps(id, can, onDrop, open)} aria-label={label} title={label + (can ? ` · ${t('timeline.dragHint')}` : '')}
          className={cn('absolute overflow-hidden rounded-sm border text-left focus-visible:ring-2', shift?.id === id && 'ring-2 ring-primary')}
          style={{ ...dragProps(id, can, onDrop, open).style, left: left + dx(id), width: w, top, height, borderColor: tone(status), background: toneBg(status) }}>
          <span className="absolute inset-y-0 left-0 opacity-40" style={{ width: `${pct}%`, background: tone(status) }} />
          {span.assumed && <span aria-hidden className="absolute inset-y-0 left-0 bg-card/65" style={{ width: assumedWidth(span, now, px) }} />}
          {!thin && w > 44 && <span className="relative px-1 text-xs/4 tabular-nums">{pct}%</span>}
        </button>
        {shift?.id === id && <span className="absolute z-30 whitespace-nowrap rounded-md bg-primary px-1.5 text-xs/[18px] text-primary-foreground tabular-nums" style={{ left: left + dx(id) + w + 4, top: top - 1 }}>{shift.days > 0 ? '+' : ''}{shift.days} d</span>}
        {span.late && <span aria-hidden className="absolute rounded-r-sm border border-l-0 border-bad" title={t('timeline.overdueBy', { n: daysBetween(span.end, now) })}
          style={{ left: x(span.end) + px, width: Math.max(daysBetween(span.end, now) * px, 2), top, height, background: HATCH }} />}
      </>
    )
  }

  return (
    <Page {...header} actions={<Button variant="outline" className="no-print" onClick={() => window.print()}><Printer className="size-4" />{t('wc.print')}</Button>}>
      <FilterBar className="no-print" activeCount={tokens.length}
        quick={<ZoomMenu value={zoom} onChange={(z) => set('zoom', z === 'month' ? null : z)} />}
        active={<ActiveFilters tokens={tokens.map(([key, label, value]) => ({ key, label, value: value ?? t('common.dash') }))} onRemove={(k) => set(k, null)} onClear={clear} />}
        actions={<>
          <Legend>{t('sig.timelineDrag')}</Legend>
          {expandable.length > 0 && <Button variant="outline" onClick={() => setExpanded(allOpen ? new Set() : new Set(expandable))}>
            {allOpen ? <ChevronsDownUp className="size-4" /> : <ChevronsUpDown className="size-4" />}{allOpen ? t('timeline.collapseTasks') : t('timeline.expandTasks')}</Button>}
        </>}>
        <div className="flex flex-wrap items-end gap-3">
          <Field label={t('common.discipline')} htmlFor="tl-discipline" className="w-full sm:w-44">
            <select id="tl-discipline" className={selectCls} value={f.disciplineId ?? ''} onChange={(e) => set('disciplineId', e.target.value)}>
              <option value="">{t('projects.anyDiscipline')}</option>{p.disciplines.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
          </Field>
          <Field label={t('field.MilestoneId')} htmlFor="tl-milestone" className="w-full sm:w-56">
            <select id="tl-milestone" className={selectCls} value={f.milestoneId ?? ''} onChange={(e) => set('milestoneId', e.target.value)}>
              <option value="">{t('deliverable.anyMilestone')}</option>{ms.data.filter((m) => !m.isCancelled).map((m) => <option key={m.id} value={m.id}>{m.key} {m.name}</option>)}
            </select>
          </Field>
          <Field label={t('common.status')} htmlFor="tl-status" className="w-full sm:w-44">
            <select id="tl-status" className={selectCls} value={f.status ?? ''} onChange={(e) => set('status', e.target.value)}>
              <option value="">{t('deliverable.anyStatus')}</option>{STATUSES.map((s) => <option key={s} value={s}>{tv(s)}</option>)}
            </select>
          </Field>
          <Field label={t('common.from')} htmlFor="tl-from" className="w-full sm:w-40"><Input id="tl-from" type="date" value={f.from ?? ''} onChange={(e) => set('from', e.target.value)} /></Field>
          <Field label={t('common.to')} htmlFor="tl-to" className="w-full sm:w-40"><Input id="tl-to" type="date" value={f.to ?? ''} onChange={(e) => set('to', e.target.value)} /></Field>
          <label className="flex min-h-(--control-h) items-center gap-2 text-sm"><Checkbox checked={f.hide} onCheckedChange={(c) => set('hideCompleted', c ? 'true' : null)} />{t('timeline.hideCompleted')}</label>
        </div>
      </FilterBar>
      <PrintLegend />
      {data.permissions.manageMilestones && <DesktopOnly notice={<Notice icon={Monitor} title={t('milestone.phoneTitle')}>{t('milestone.phoneHint')}</Notice>}>{null}</DesktopOnly>}
      {rows.length === 0 && milestones.length === 0 ? (
        <div className="rounded-lg border bg-card"><Empty action={tokens.length > 0 && <Button variant="outline" onClick={clear}>{t('filters.clear')}</Button>}>{t('timeline.empty')}</Empty></div>
      ) : (
        <div className="scroll-region overflow-x-auto rounded-lg border bg-card print:overflow-visible" role="region" aria-label={t('ptab.timeline')}>
          {/* A table of rows: the item (row header) and its dates (cell), so each bar is announced with its row. */}
          <div className="relative" style={{ width: LABEL + width }} role="table" aria-label={t('timeline.items')}>
            <div className="flex h-10 border-b bg-muted text-xs/4 text-muted-foreground" role="row">
              <div className="sticky left-0 z-20 flex shrink-0 items-center border-r bg-muted px-3 text-sm font-medium" style={{ width: LABEL }} role="columnheader">{t('timeline.items')}</div>
              <div className="relative" style={{ width }} role="columnheader" aria-label={t('sig.dates')}>
                {ticks.map((k) => (
                  <div key={k.date} aria-hidden className={cn('absolute top-0 h-full border-l', k.major ? 'border-border' : 'border-border/40')} style={{ left: x(k.date) }}>
                    {k.label && <span className="absolute left-1 top-1 whitespace-nowrap tabular-nums">{k.label}</span>}
                  </div>
                ))}
              </div>
            </div>
            <div className="flex border-b" style={{ height: laneHeight }} role="row">
              <div className="sticky left-0 z-20 flex shrink-0 items-center border-r bg-card px-3 text-sm font-semibold" style={{ width: LABEL }} role="rowheader">{t('ptab.milestones')}</div>
              <div className="relative" style={{ width }} role="cell">
                {placed.map(({ m, left, w, row }) => {
                  const status = milestoneStatusLabel(m)
                  const slipped = m.originalDate && m.originalDate !== m.date && !m.isComplete && visible(m.originalDate)
                  const can = data.permissions.manageMilestones && !m.isComplete && !phone
                  const drop = (n: number) => setMsMove({ m, date: addDays(m.date!, n) })
                  return (
                    <Fragment key={m.id}>
                      {slipped && <span aria-hidden title={t('timeline.originalDate', { date: fmtDate(m.originalDate) })} className="absolute top-[7px] size-3 rotate-45 border-2 bg-card"
                        style={{ left: x(m.originalDate!) - 6 + px / 2, borderColor: tone(status) }} />}
                      <button type="button" {...dragProps(m.id, can, drop, () => openPanel('Milestone', m.id))} className="group absolute top-px grid size-6 -translate-x-1/2 place-items-center rounded-md"
                        style={{ ...dragProps(m.id, can, drop, () => openPanel('Milestone', m.id)).style, left: left + px / 2 + dx(m.id) }}
                        aria-label={t('timeline.milestoneLabel', { key: m.key, name: m.name, date: fmtDate(m.date), status: tv(status) })} title={`${m.key} ${m.name} · ${fmtDate(m.date)} · ${tv(status)}`}>
                        <span className={cn('block size-4 rotate-45 border border-white shadow-[0_0_0_1px_rgba(25,27,32,0.3)]', shift?.id === m.id && 'ring-2 ring-primary')} style={{ background: tone(status) }} />
                      </button>
                      {shift?.id === m.id && <span className="absolute z-30 whitespace-nowrap rounded-md bg-primary px-1.5 text-xs/[18px] text-primary-foreground tabular-nums" style={{ left: left + px / 2 + dx(m.id) + 12, top: 3 }}>{shift.days > 0 ? '+' : ''}{shift.days} d</span>}
                      <span className="pointer-events-none absolute truncate text-xs/4" style={{ left: left + px / 2 - 6, top: 24 + row * 16, maxWidth: w }} title={m.name}>
                        {m.name} · <span className="text-muted-foreground tabular-nums">{shortDate(m.date)}</span>
                      </span>
                    </Fragment>
                  )
                })}
              </div>
            </div>
            <div className="relative" role="rowgroup">
              {rows.map((r) => {
                if (r.kind === 'group') return (
                  <div key={r.id} className="flex border-b bg-muted" style={{ height: H.group }} role="row">
                    <div role="rowheader" className="sticky left-0 z-20 flex shrink-0 border-r bg-muted" style={{ width: LABEL }}>
                      <button type="button" className="flex w-full items-center gap-2 px-2 text-left text-sm font-semibold hover:bg-secondary"
                        aria-expanded={!closed.has(r.id)} onClick={() => toggle(setClosed, r.id)}>
                        {closed.has(r.id) ? <ChevronRight className="size-4 shrink-0" /> : <ChevronDown className="size-4 shrink-0" />}
                        <span className="inline-block size-2.5 shrink-0 rounded-sm" style={{ background: disciplineColour(r.colour) }} aria-hidden /><span className="truncate">{r.name}</span>
                        <span className="rounded-md bg-card px-1.5 text-xs/[18px] font-medium text-muted-foreground tabular-nums">{r.count}</span>
                      </button>
                    </div>
                    <div style={{ width }} role="cell" />
                  </div>
                )
                if (r.kind === 'loose') return (
                  <div key={r.id} className="flex border-b" style={{ height: H.loose }} role="row">
                    <div role="rowheader" className="sticky left-0 z-20 flex shrink-0 border-r bg-card" style={{ width: LABEL }}>
                      <button type="button" className="flex w-full items-center gap-1.5 px-3 text-left text-xs/4 text-muted-foreground hover:bg-muted"
                        aria-expanded={expanded.has(r.id)} onClick={() => toggle(setExpanded, r.id)} title={t('sig.looseHint')}>
                        {expanded.has(r.id) ? <ChevronDown className="size-3.5 shrink-0" /> : <ChevronRight className="size-3.5 shrink-0" />}<span className="truncate">{t('sig.looseTasks', { n: r.count })}</span>
                      </button>
                    </div>
                    <div style={{ width }} role="cell" />
                  </div>
                )
                if (r.kind === 'deliverable') {
                  const d = r.d
                  const pct = d.state?.progressPct ?? 0
                  const can = !!canDel.get(d.id)
                  return (
                    <div key={r.id} className="flex border-b" style={{ height: H.deliverable }} role="row">
                      <div className="sticky left-0 z-20 flex shrink-0 items-center gap-1 border-r bg-card pl-1 pr-3 text-sm" style={{ width: LABEL }} role="rowheader">
                        {r.tasks > 0 ? <button type="button" className="grid size-6 shrink-0 place-items-center rounded-md hover:bg-muted" aria-expanded={expanded.has(d.id)} aria-label={t('timeline.showTasks', { key: d.key, n: r.tasks })} onClick={() => toggle(setExpanded, d.id)}>
                          {expanded.has(d.id) ? <ChevronDown className="size-4" /> : <ChevronRight className="size-4" />}</button> : <span className="w-6 shrink-0" />}
                        <button type="button" className="flex min-w-0 flex-1 items-center gap-2 text-left hover:underline" onClick={() => openPanel('Deliverable', d.id)} title={`${d.key} ${d.name}`}>
                          <Key>{d.key.split('-').pop()}</Key><span className="truncate">{d.name}</span>
                        </button>
                      </div>
                      <div className="relative" style={{ width }} role="cell">
                        {bar(d.id, r.span, d.status, pct, t('timeline.barLabel', { key: d.key, name: d.name, from: fmtDate(r.span.start), to: fmtDate(r.span.end), status: tv(d.status), pct }), false, can,
                          (n) => setMove({ kind: 'deliverable', id: d.id, key: d.key, name: d.name, start: d.startDate, due: d.dueDate!, days: n, rowVersion: d.rowVersion, needsReason: false }),
                          () => openPanel('Deliverable', d.id))}
                      </div>
                    </div>
                  )
                }
                const tk = r.tk
                return (
                  <div key={r.id} className="flex border-b border-border/60" style={{ height: H.task }} role="row">
                    <div role="rowheader" className="sticky left-0 z-20 flex shrink-0 border-r bg-card" style={{ width: LABEL }}>
                      <button type="button" className="flex w-full min-w-0 items-center gap-2 pl-8 pr-3 text-left text-xs/4 hover:bg-muted"
                        onClick={() => openPanel('Task', tk.id)} title={`${tk.key} ${tk.name}${tk.assignee ? ` · ${tk.assignee}` : ''}`}>
                        <span className="key text-muted-foreground">{tk.key.split('-').pop()}</span><span className="truncate">{tk.name}</span>
                      </button>
                    </div>
                    <div className="relative" style={{ width }} role="cell">
                      {bar(tk.id, r.span, tk.status, tk.progressPct, t('timeline.barLabel', { key: tk.key, name: tk.name, from: fmtDate(r.span.start), to: fmtDate(r.span.end), status: tv(tk.status), pct: tk.progressPct }), true, tk.canEditDates,
                        (n) => setMove({ kind: 'task', id: tk.id, key: tk.key, name: tk.name, start: tk.startDate, due: tk.dueDate!, days: n, rowVersion: tk.rowVersion, needsReason: tk.dueNeedsReason }),
                        () => openPanel('Task', tk.id))}
                    </div>
                  </div>
                )
              })}
              {arrows.length > 0 && (
                <svg aria-hidden className="pointer-events-none absolute top-0 z-10" style={{ left: LABEL, width, height: bodyHeight }} width={width} height={bodyHeight}>
                  <defs>
                    {(['hot', 'cold'] as const).map((k) => (
                      <marker key={k} id={`arrow-${k}`} viewBox="0 0 8 8" refX="7" refY="4" markerWidth="7" markerHeight="7" orient="auto-start-reverse">
                        <path d="M0,0 L8,4 L0,8 z" fill={k === 'hot' ? 'var(--bad)' : 'var(--muted-foreground)'} fillOpacity={k === 'hot' ? 1 : 0.55} />
                      </marker>
                    ))}
                  </defs>
                  {arrows}
                </svg>
              )}
            </div>
            {lines.map((l) => (
              <div key={l.label} aria-hidden className={cn('pointer-events-none absolute bottom-0 top-0 z-10', l.cls)} style={{ left: LABEL + x(l.date) + l.offset }}>
                <span className={cn('absolute left-1 top-[22px] whitespace-nowrap rounded-sm bg-card/90 px-1 text-xs/4 font-semibold', l.text)}>{l.label}</span>
              </div>
            ))}
          </div>
        </div>
      )}
      {unscheduled.length > 0 && (
        <Section id="tl-unscheduled" title={t('timeline.unscheduled')} count={unscheduled.length}>
          <ul className="divide-y text-sm">{unscheduled.map((u) => (
            <li key={u.id}><button type="button" className="flex min-h-(--row-min) w-full items-center gap-3 px-5 py-(--cell-py) text-left hover:bg-muted" onClick={() => openPanel(u.type, u.id)}>
              <span className="w-24 shrink-0 text-xs/[18px] text-muted-foreground">{t(`itemType.${u.type}`)}</span><Key>{u.key}</Key><span className="min-w-0 flex-1 truncate font-medium">{u.name}</span>
              <span className="text-xs/[18px] text-muted-foreground">{u.sub}</span></button></li>))}</ul>
        </Section>
      )}
      {move && <MoveDialog move={move} projectComplete={data.permissions.needsReason} onClose={(ok) => { setMove(null); if (ok) reload() }} />}
      {msMove && <ChangeDateDialog m={msMove.m} initialDate={msMove.date} onClose={() => setMsMove(null)} onDone={() => { setMsMove(null); reload() }} />}
    </Page>
  )
}

/** The confirmation before a dragged date is saved (§12.16, §36.4): the old and new dates, and that nothing else moves. */
export function MoveDialog({ move, projectComplete, onClose }: { move: Move; projectComplete: boolean; onClose: (ok: boolean) => void }) {
  const newStart = move.start ? addDays(move.start, move.days) : null
  const newDue = addDays(move.due, move.days)
  const reason = move.needsReason || projectComplete
  return (
    <ConfirmDialog open onOpenChange={(o) => !o && onClose(false)} reason={reason ? true : undefined} title={t('timeline.moveTitle', { key: move.key, name: move.name })}
      confirmLabel={t('timeline.moveConfirm')}
      body={<>
        <p>{t('timeline.moveBy', { n: move.days > 0 ? `+${move.days}` : move.days })}</p>
        <ul className="list-disc pl-5">
          {move.start && <li>{t('timeline.startChange', { from: fmtDate(move.start), to: fmtDate(newStart) })}</li>}
          <li>{t('timeline.dueChange', { from: fmtDate(move.due), to: fmtDate(newDue) })}</li>
        </ul>
        <p className="text-muted-foreground">{t('timeline.onlyThis')}</p>
      </>}
      onConfirm={async (why) => {
        const body: Record<string, unknown> = { dueDate: newDue, reason: why || undefined }
        if (newStart) body.startDate = newStart
        await patch(`${move.kind === 'task' ? 'tasks' : 'deliverables'}/${move.id}`, body, move.rowVersion)
        toast.success(t('timeline.moved', { key: move.key }))
        onClose(true)
      }} />
  )
}

/** The legend's marks: what a bar spans, the assumed start (§40 AC-TL-01), status, overdue, moved dates and link types. */
function LegendItems({ className }: { className: string }) {
  const swatch = (status: string) => <span className="inline-block h-3 w-5 rounded-sm border" style={{ borderColor: tone(status), background: toneBg(status) }} aria-hidden />
  const arrow = (d: string, head: string) => <svg width="22" height="14" aria-hidden><path d={d} fill="none" stroke="var(--muted-foreground)" strokeWidth="1.2" /><path d={head} fill="var(--muted-foreground)" /></svg>
  return (
    <ul className={className}>
      <li className="flex items-center gap-2"><span className="inline-block h-3 w-5 rounded-sm border border-foreground/50 bg-muted" aria-hidden />{t('sig.legendBar')}</li>
      <li className="flex items-center gap-2"><span className="relative inline-block h-3 w-5 overflow-hidden rounded-sm border" style={{ borderColor: tone('In Progress'), background: toneBg('In Progress') }} aria-hidden><span className="absolute inset-y-0 left-0 w-3 bg-card/65" /></span>{t('sig.legendAssumed')}</li>
      {['Not Started', 'In Progress', 'Revision Required', 'Issued'].map((st) => <li key={st} className="flex items-center gap-2">{swatch(st)}{tv(st)}</li>)}
      <li className="flex items-center gap-2"><span className="inline-block h-3 w-5 rounded-sm border border-bad" style={{ background: HATCH }} aria-hidden />{t('timeline.legendOverdue')}</li>
      <li className="flex items-center gap-2"><span className="inline-block h-3 w-5 rounded-sm border border-dashed border-foreground/40" aria-hidden />{t('timeline.legendBaseline')}</li>
      <li className="flex items-center gap-2"><span className="mx-1 inline-block size-2.5 rotate-45" style={{ background: 'var(--ok)' }} aria-hidden />{t('timeline.legendMilestone')}</li>
      <li className="flex items-center gap-2"><span className="mx-1 inline-block size-2.5 rotate-45 border-2" style={{ borderColor: 'var(--warn)' }} aria-hidden />{t('timeline.legendOriginal')}</li>
      <li className="flex items-center gap-2"><svg width="22" height="8" aria-hidden><path d="M1 4 H18" stroke="var(--bad)" strokeWidth="1.6" /><path d="M16 1 L21 4 L16 7 z" fill="var(--bad)" /></svg>{t('timeline.legendLate')}</li>
      <li className="flex items-center gap-2">{arrow('M2 3 C 12 3, 8 11, 17 11', 'M16 8 L21 11 L16 14 z')}{t('sig.dep.FinishToStart')}</li>
      <li className="flex items-center gap-2">{arrow('M8 3 C 1 3, 1 11, 11 11', 'M10 8 L15 11 L10 14 z')}{t('sig.dep.StartToStart')}</li>
      <li className="flex items-center gap-2">{arrow('M10 3 C 21 3, 21 11, 15 11', 'M16 8 L11 11 L16 14 z')}{t('sig.dep.FinishToFinish')}</li>
      <li className="flex items-center gap-2"><span className="mx-2 inline-block h-3 border-l-2 border-primary" aria-hidden />{t('common.today')}</li>
    </ul>
  )
}

/** The legend behind a Legend button (§41 R1). `children` adds the page's own help, such as how dragging works. */
export function Legend({ children }: { children?: ReactNode }) {
  return (
    <Popover>
      <PopoverTrigger asChild><Button variant="outline"><ListTree className="size-4" aria-hidden />{t('timeline.legend')}</Button></PopoverTrigger>
      <PopoverContent align="end" aria-label={t('timeline.legend')} className="w-80">
        <LegendItems className="space-y-2 text-sm" />
        <p className="mt-3 text-xs/[18px] text-muted-foreground">{t('sig.legendArrows')}</p>
        {children && <p className="mt-2 text-xs/[18px] text-muted-foreground">{children}</p>}
      </PopoverContent>
    </Popover>
  )
}

/** The legend on paper, where the popover cannot open. */
export const PrintLegend = () => <div className="hidden print:block"><LegendItems className="flex flex-wrap items-center gap-x-5 gap-y-1.5 text-xs/[18px] text-muted-foreground" /></div>
