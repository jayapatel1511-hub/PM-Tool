import { useQuery } from '@tanstack/react-query'
import { ChevronDown, ChevronLeft, ChevronRight, ChevronsDownUp, ChevronsUpDown } from 'lucide-react'
import { Fragment, useRef, useState, type PointerEvent as ReactPointerEvent } from 'react'
import { Link, useSearchParams } from 'react-router'
import { AccentDot, ActiveFilters, Empty, ErrorBanner, FilterBar, Loading, Notice, Page, Section, Segmented } from '@/components/hub/common'
import { useItemPanel } from '@/components/hub/panel-host'
import { Key } from '@/components/hub/pills'
import { useScope, WorkspaceTabs } from '@/components/hub/workspace'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { get, qs } from '@/lib/api'
import { addDays, daysBetween, fmtDate, monthLabel, today } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import { accentOf, cn } from '@/lib/utils'
import { HATCH, Legend, MoveDialog, ZOOM, tone, toneBg, type Move, type Zoom } from './projects/Timeline'

interface Dated { id: string; key: string; name: string; startDate?: string | null; dueDate?: string | null; originalStartDate?: string | null; originalDueDate?: string | null
  status: string; progressPct: number; rowVersion: number; createdOn: string; isOverdue: boolean; canEditDates: boolean }
interface GTask extends Dated { deliverableId?: string | null; assignee?: string | null; dueNeedsReason: boolean }
interface GProject {
  project: { id: string; projectNumber: string; name: string; status: string; startDate?: string | null; targetCompletionDate?: string | null }
  data: { permissions: { needsReason: boolean }; tasks: GTask[]; deliverables: Dated[]
    dependencies: { id: string; predecessorTaskId: string; successorTaskId: string; highlighted: boolean }[]
    milestones: { id: string; key: string; name: string; date: string; isComplete: boolean; status: string }[] }
}
interface Span { start: string; end: string; late: boolean; ghost: { start: string; end: string } | null }
type Row =
  | { kind: 'project'; id: string; g: GProject; count: number }
  | { kind: 'deliverable' | 'task'; id: string; g: GProject; item: Dated; span: Span; indent: boolean }

const LABEL = 240
const H = { project: 44, deliverable: 36, task: 32 } as const
/** Bar heights inside their rows: deliverables stand out from their tasks. */
const BAR = { deliverable: 22, task: 18 } as const
/** Days shown per zoom; previous and next move by half of it (FR-VIS-05). */
const LEN: Record<Zoom, number> = { week: 42, month: 119, quarter: 364 }
const CLOSED = ['Issued', 'Accepted', 'Cancelled']
const monday = (d: string) => addDays(d, -((new Date(d + 'T00:00:00Z').getUTCDay() + 6) % 7))

/** Cross-project Timeline (§36.4, FR-VIS-05): the selected projects as expandable groups with their deliverables and tasks
 *  as dated bars, milestone diamonds, arrows only for each project's own dependencies, and a today line. A drag previews
 *  the old and new dates before saving; cancelling changes nothing. Undated items stay listed under Unscheduled. */
export function GanttPage() {
  const scope = useScope()
  const [sp, setSp] = useSearchParams()
  const openPanel = useItemPanel()
  const q = useQuery({ queryKey: ['gantt', scope.api], enabled: scope.ready, queryFn: () => get<{ projects: GProject[]; truncated: boolean; max: number }>(`timeline${qs({ projects: scope.api })}`) })
  const [closed, setClosed] = useState<Set<string>>(new Set())
  const [shift, setShift] = useState<{ id: string; days: number } | null>(null)
  const [move, setMove] = useState<(Move & { projectComplete: boolean }) | null>(null)
  const drag = useRef<{ id: string; x0: number; moved: boolean } | null>(null)
  const suppress = useRef(false)
  const set = (k: string, v?: string | null) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); setSp(n, { replace: true }) }
  const zoom = (sp.get('zoom') as Zoom | null) ?? 'month'
  const hide = sp.get('hideCompleted') === 'true'
  const now = today()
  const start = sp.get('start') ?? addDays(monday(now), -14)
  const end = addDays(start, LEN[zoom] - 1)
  const px = ZOOM[zoom]
  const width = Math.round(LEN[zoom] * px)
  const x = (d: string) => daysBetween(start, d) * px

  if (!scope.ready || q.isPending) return <Page title={t('wsview.timeline')}><WorkspaceTabs /><div className="rounded-lg border bg-card"><Loading rows={8} /></div></Page>
  if (q.error) return <Page title={t('wsview.timeline')}><WorkspaceTabs /><ErrorBanner error={q.error} retry={() => q.refetch()} /></Page>

  const spanOf = (i: Dated): Span => {
    const due = i.dueDate!
    const b = i.startDate ?? i.createdOn
    const ghost = i.originalDueDate && (i.originalDueDate !== i.dueDate || (i.originalStartDate ?? null) !== (i.startDate ?? null)) ? { start: i.originalStartDate ?? i.originalDueDate, end: i.originalDueDate } : null
    return { start: b > due ? due : b, end: due, late: i.isOverdue && due < now, ghost }
  }
  const rows: Row[] = []
  const unscheduled: { type: 'Task' | 'Deliverable'; id: string; key: string; name: string; project: string; projectId: string }[] = []
  for (const g of q.data.projects) {
    const tasks = g.data.tasks.filter((tk) => !hide || tk.status !== 'Complete')
    const dels = g.data.deliverables.filter((d) => !hide || !CLOSED.includes(d.status))
    const where = { project: g.project.projectNumber, projectId: g.project.id }
    unscheduled.push(...dels.filter((d) => !d.dueDate).map((d) => ({ type: 'Deliverable' as const, id: d.id, key: d.key, name: d.name, ...where })),
      ...tasks.filter((tk) => !tk.dueDate).map((tk) => ({ type: 'Task' as const, id: tk.id, key: tk.key, name: tk.name, ...where })))
    const datedDels = dels.filter((d) => d.dueDate).sort((a, b) => a.dueDate!.localeCompare(b.dueDate!))
    const datedTasks = tasks.filter((tk) => tk.dueDate).sort((a, b) => (a.startDate ?? a.dueDate!).localeCompare(b.startDate ?? b.dueDate!))
    rows.push({ kind: 'project', id: g.project.id, g, count: datedDels.length + datedTasks.length })
    if (closed.has(g.project.id)) continue
    for (const d of datedDels) {
      rows.push({ kind: 'deliverable', id: d.id, g, item: d, span: spanOf(d), indent: false })
      for (const tk of datedTasks.filter((x) => x.deliverableId === d.id)) rows.push({ kind: 'task', id: tk.id, g, item: tk, span: spanOf(tk), indent: true })
    }
    for (const tk of datedTasks.filter((x) => !x.deliverableId || !datedDels.some((d) => d.id === x.deliverableId))) rows.push({ kind: 'task', id: tk.id, g, item: tk, span: spanOf(tk), indent: false })
  }

  // The axis: months over week starts (week and month zoom), or years over months (quarter zoom). The first period is
  // labelled at the range start unless the next boundary is too close for its label.
  const dates = Array.from({ length: LEN[zoom] }, (_, i) => addDays(start, i))
  const quarter = zoom === 'quarter'
  const majors = dates.filter((d) => (quarter ? d.endsWith('-01-01') : d.endsWith('-01')))
  const minors = dates.filter((d) => (quarter ? d.endsWith('-01') : new Date(d + 'T00:00:00Z').getUTCDay() === 1))
  const upper = majors[0] !== undefined && x(majors[0]) < 72 ? majors : [start, ...majors]
  const upperLabel = (d: string) => (quarter ? d.slice(0, 4) : monthLabel(d))
  const lowerLabel = (d: string) => (quarter ? monthLabel(d).split(' ')[0] : String(Number(d.slice(8))))
  const showToday = now >= start && now <= end

  const pos = new Map<string, { y: number; h: number }>()
  let y = 0
  for (const r of rows) { const h = H[r.kind]; pos.set(r.id, { y, h }); y += h }
  const spans = new Map(rows.flatMap((r) => (r.kind === 'task' ? [[r.id, r.span] as const] : [])))
  const arrows = q.data.projects.flatMap((g) => g.data.dependencies.map((dep) => {
    const a = pos.get(dep.predecessorTaskId), b = pos.get(dep.successorTaskId), sa = spans.get(dep.predecessorTaskId), sb = spans.get(dep.successorTaskId)
    if (!a || !b || !sa || !sb) return null
    const x1 = x(sa.end) + px, y1 = a.y + a.h / 2, x2 = x(sb.start), y2 = b.y + b.h / 2
    const bend = Math.max(12, Math.abs(x2 - x1) / 3)
    return <path key={dep.id} d={`M ${x1} ${y1} C ${x1 + bend} ${y1}, ${x2 - bend} ${y2}, ${x2 - 2} ${y2}`} fill="none" strokeWidth={dep.highlighted ? 1.6 : 1}
      stroke={dep.highlighted ? 'var(--bad)' : 'var(--muted-foreground)'} strokeOpacity={dep.highlighted ? 1 : 0.55} markerEnd={`url(#g-arrow-${dep.highlighted ? 'hot' : 'cold'})`} />
  })).filter(Boolean)

  /** Pointer drag snapping to days; a click without movement opens the item. */
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
  })

  const bar = (r: Extract<Row, { kind: 'deliverable' | 'task' }>) => {
    const { item, span } = r
    const left = x(span.start), right = x(span.end) + px
    if (right < 0 || left > width) return null
    const dx = shift?.id === item.id ? shift.days * px : 0
    const type = r.kind === 'task' ? 'Task' : 'Deliverable'
    const late = span.late ? daysBetween(span.end, now) : 0
    const slip = span.ghost ? daysBetween(span.ghost.end, span.end) : 0
    // Overdue and moved dates are spoken as well as drawn (hatch, dashed baseline), never colour alone.
    const label = [t('timeline.barLabel', { key: item.key, name: item.name, from: fmtDate(span.start), to: fmtDate(span.end), status: tv(item.status), pct: item.progressPct }),
      late > 0 && t('timeline.overdueBy', { n: late }), span.ghost && t('timeline.baseline', { from: fmtDate(span.ghost.start), to: fmtDate(span.ghost.end) })].filter(Boolean).join(' · ')
    const drop = (n: number) => setMove({ kind: r.kind, id: item.id, key: item.key, name: item.name, start: item.startDate, due: item.dueDate!, days: n, rowVersion: item.rowVersion,
      needsReason: r.kind === 'task' && (item as GTask).dueNeedsReason, projectComplete: r.g.data.permissions.needsReason })
    const props = dragProps(item.id, item.canEditDates, drop, () => openPanel(type, item.id))
    const height = BAR[r.kind], top = (H[r.kind] - height) / 2, w = Math.max(right - left, 4)
    const after = (late > 0 ? right + Math.max(late * px, 2) : right) + 6
    return (
      <>
        {span.ghost && <span aria-hidden className="absolute rounded-md border border-dashed border-foreground/50" title={t('timeline.baseline', { from: fmtDate(span.ghost.start), to: fmtDate(span.ghost.end) })}
          style={{ left: x(span.ghost.start), width: Math.max((daysBetween(span.ghost.start, span.ghost.end) + 1) * px, 4), top: top - 3, height: height + 6 }} />}
        <button type="button" {...props} aria-label={label} title={label + (item.canEditDates ? ` · ${t('timeline.dragHint')}` : '')}
          className={cn('absolute flex items-center overflow-hidden rounded-md border text-left text-xs/4 text-foreground', span.late && 'rounded-r-none', dx && 'ring-2 ring-primary')}
          style={{ left: left + dx, width: w, top, height, borderColor: tone(item.status), background: toneBg(item.status), touchAction: item.canEditDates ? 'none' : undefined, cursor: item.canEditDates ? 'grab' : 'pointer' }}>
          {w >= 40 && <span className="min-w-0 truncate px-1.5 font-medium tabular-nums">{item.progressPct}%</span>}
          <span data-meter-fill className="absolute bottom-0 left-0 h-[3px]" style={{ width: `${item.progressPct}%`, background: tone(item.status) }} />
        </button>
        {dx !== 0 && <span className="absolute z-30 rounded-md bg-primary px-1.5 text-xs/[18px] font-medium text-primary-foreground tabular-nums" style={{ left: right + dx + 4, top }}>{shift!.days > 0 ? '+' : ''}{shift!.days} d</span>}
        {span.late && <span aria-hidden className="absolute rounded-r-md border border-l-0 border-bad" title={t('timeline.overdueBy', { n: late })}
          style={{ left: right, width: Math.max(late * px, 2), top, height, background: HATCH }} />}
        {dx === 0 && slip !== 0 && <span aria-hidden className="absolute whitespace-nowrap text-xs/[18px] text-muted-foreground tabular-nums" style={{ left: after, top: (H[r.kind] - 18) / 2 }}>
          {t('gantt.slip', { n: slip > 0 ? `+${slip}` : `−${-slip}` })}</span>}
      </>
    )
  }

  const expandable = q.data.projects.map((g) => g.project.id)
  const allOpen = closed.size === 0
  const move2 = (n: number) => set('start', addDays(start, n))
  return (
    <Page title={t('wsview.timeline')} subtitle={t('gantt.range', { from: fmtDate(start), to: fmtDate(end) })}>
      <WorkspaceTabs />
      <FilterBar className="no-print">
        <div className="flex flex-wrap items-center gap-3">
          <Segmented label={t('timeline.zoom')} value={zoom} onChange={(z) => set('zoom', z === 'month' ? null : z)}
            options={(Object.keys(ZOOM) as Zoom[]).map((z) => ({ value: z, label: t(`timeline.zoom.${z}`) }))} />
          <div className="flex items-center gap-2" role="group" aria-label={t('gantt.navigate')}>
            <Button variant="outline" size="icon" aria-label={t('common.previous')} onClick={() => move2(-Math.round(LEN[zoom] / 2))}><ChevronLeft className="size-5" /></Button>
            <Button variant="outline" onClick={() => set('start', null)}>{t('common.today')}</Button>
            <Button variant="outline" size="icon" aria-label={t('common.next')} onClick={() => move2(Math.round(LEN[zoom] / 2))}><ChevronRight className="size-5" /></Button>
          </div>
          <span className="text-sm font-semibold tabular-nums" aria-live="polite">{fmtDate(start)} – {fmtDate(end)}</span>
          <label className="flex min-h-(--control-h) items-center gap-2 text-sm"><Checkbox checked={hide} onCheckedChange={(c) => set('hideCompleted', c ? 'true' : null)} />{t('timeline.hideCompleted')}</label>
          {expandable.length > 0 && <Button variant="ghost" onClick={() => setClosed(allOpen ? new Set(expandable) : new Set())}>
            {allOpen ? <ChevronsDownUp className="size-4" /> : <ChevronsUpDown className="size-4" />}{allOpen ? t('gantt.collapse') : t('gantt.expand')}</Button>}
        </div>
        <ActiveFilters tokens={hide ? [{ key: 'hideCompleted', label: t('timeline.hideCompleted'), value: t('common.yes') }] : []} onRemove={(k) => set(k, null)} onClear={() => set('hideCompleted', null)} />
      </FilterBar>
      <Legend />
      {q.data.truncated && <Notice title={t('ws.truncatedTitle')}>{t('gantt.truncated', { n: q.data.max })}</Notice>}
      {rows.length === 0 ? <div className="rounded-lg border bg-card"><Empty>{t('gantt.empty')}</Empty></div> : (
        // eslint-disable-next-line jsx-a11y/no-noninteractive-tabindex -- axe scrollable-region-focusable: keyboard users scroll the timeline
        <div className="scroll-region max-h-[75dvh] overflow-auto rounded-lg border bg-card print:max-h-none print:overflow-visible" role="region" aria-label={t('wsview.timeline')} tabIndex={0}>
          <div className="relative" style={{ width: LABEL + width }}>
            {/* Time axis, kept in view while the rows scroll; the item column stays put while the dates scroll. */}
            <div className="sticky top-0 z-30 flex border-b bg-muted text-xs/[18px] text-muted-foreground">
              <div className="sticky left-0 z-10 flex shrink-0 items-end border-r bg-muted px-3 py-1.5 font-medium" style={{ width: LABEL }}>{t('timeline.items')}</div>
              <div className="relative h-12 overflow-hidden" style={{ width }} aria-hidden>
                {upper.map((d) => <span key={`u${d}`} className="absolute top-0 flex h-6 items-center whitespace-nowrap border-l pl-1.5 font-semibold text-foreground" style={{ left: x(d) }}>{upperLabel(d)}</span>)}
                {minors.map((d) => <span key={`l${d}`} className="absolute top-6 flex h-6 items-center whitespace-nowrap border-l pl-1 tabular-nums" style={{ left: x(d) }}>{lowerLabel(d)}</span>)}
                {showToday && <span className="absolute top-6 z-10 my-0.5 -translate-x-1/2 rounded-md bg-primary px-1.5 font-medium text-primary-foreground" style={{ left: x(now) + px / 2 }}>{t('common.today')}</span>}
              </div>
            </div>
            <div className="relative">
              <div aria-hidden className="pointer-events-none absolute inset-y-0" style={{ left: LABEL, width }}>
                {minors.map((d) => <span key={d} className="absolute inset-y-0 border-l border-border/70" style={{ left: x(d) }} />)}
                {majors.map((d) => <span key={d} className="absolute inset-y-0 border-l border-input/40" style={{ left: x(d) }} />)}
              </div>
              {rows.map((r) => {
                if (r.kind === 'project') {
                  const p = r.g.project
                  const open = !closed.has(p.id)
                  const ps = p.startDate && p.targetCompletionDate ? { l: Math.max(x(p.startDate), 0), r: Math.min(x(p.targetCompletionDate) + px, width) } : null
                  const ms = r.g.data.milestones.filter((m) => m.date >= start && m.date <= end).sort((a, b) => a.date.localeCompare(b.date))
                  return (
                    <div key={r.id} data-accent={accentOf(p.id)} className="flex border-b bg-muted" style={{ height: H.project }}>
                      <div className="sticky left-0 z-20 flex shrink-0 items-center gap-1 border-r bg-muted pl-1 pr-3" style={{ width: LABEL }}>
                        <Button variant="ghost" size="icon-sm" aria-expanded={open} aria-label={t(open ? 'gantt.collapseProject' : 'gantt.expandProject', { name: p.projectNumber })}
                          onClick={() => setClosed((s) => { const n = new Set(s); if (n.has(p.id)) n.delete(p.id); else n.add(p.id); return n })}>
                          {open ? <ChevronDown className="size-4" /> : <ChevronRight className="size-4" />}
                        </Button>
                        <Link to={`/projects/${p.projectNumber}/timeline`} className="flex min-w-0 flex-1 items-center gap-2 text-sm font-semibold hover:underline" title={`${p.projectNumber} ${p.name}`}>
                          <AccentDot id={p.id} /><span className="key shrink-0 font-normal text-muted-foreground">{p.projectNumber}</span><span className="truncate">{p.name}</span>
                        </Link>
                        <span className="rounded-md bg-card px-1.5 text-xs/[18px] font-medium text-muted-foreground tabular-nums">{r.count}</span>
                      </div>
                      <div className="relative overflow-hidden" style={{ width }}>
                        {ps && ps.r > ps.l && <span aria-hidden className="absolute bottom-1.5 h-1 rounded-full bg-(--acc-stripe)" style={{ left: ps.l, width: ps.r - ps.l }} title={t('gantt.projectSpan', { from: fmtDate(p.startDate), to: fmtDate(p.targetCompletionDate) })} />}
                        {ms.map((m, i) => {
                          const at = x(m.date) + px / 2
                          const room = (i + 1 < ms.length ? x(ms[i + 1].date) + px / 2 : width) - at - 24 // labels stop before the next diamond
                          return (
                            <Fragment key={m.id}>
                              <button type="button" className="absolute top-1/2 grid size-6 -translate-x-1/2 -translate-y-1/2 place-items-center rounded-md" style={{ left: at }} onClick={() => openPanel('Milestone', m.id)}
                                aria-label={t('timeline.milestoneLabel', { key: m.key, name: m.name, date: fmtDate(m.date), status: tv(m.status) })} title={`${m.key} ${m.name} · ${fmtDate(m.date)} · ${tv(m.status)}`}>
                                <span className="block size-3.5 rotate-45 border border-card" style={{ background: tone(m.status) }} />
                              </button>
                              {room >= 32 && <span aria-hidden className="pointer-events-none absolute top-1/2 -translate-y-1/2 truncate text-xs/[18px] font-medium text-foreground" style={{ left: at + 14, maxWidth: room }}>{m.name}</span>}
                            </Fragment>
                          )
                        })}
                      </div>
                    </div>
                  )
                }
                const type = r.kind === 'task' ? 'Task' : 'Deliverable'
                return (
                  <div key={r.id} className="flex border-b" style={{ height: H[r.kind] }}>
                    <button type="button" className={cn('sticky left-0 z-20 flex shrink-0 items-center gap-2 border-r bg-card pr-3 text-left text-sm outline-offset-[-2px] hover:bg-muted', r.kind === 'deliverable' && 'font-medium', r.indent ? 'pl-14' : 'pl-10')}
                      style={{ width: LABEL }} onClick={() => openPanel(type, r.item.id)} title={`${r.item.key} ${r.item.name}${r.kind === 'task' && (r.item as GTask).assignee ? ` · ${(r.item as GTask).assignee}` : ''}`}>
                      <span className="key shrink-0 font-normal text-muted-foreground">{r.item.key.split('-').pop()}</span><span className="truncate">{r.item.name}</span>
                    </button>
                    <div className="relative overflow-hidden" style={{ width }}>{bar(r)}</div>
                  </div>
                )
              })}
              {arrows.length > 0 && (
                <svg aria-hidden className="pointer-events-none absolute top-0 z-10 overflow-hidden" style={{ left: LABEL, width, height: y }} width={width} height={y}>
                  <defs>
                    {(['hot', 'cold'] as const).map((k) => (
                      <marker key={k} id={`g-arrow-${k}`} viewBox="0 0 8 8" refX="7" refY="4" markerWidth="7" markerHeight="7" orient="auto-start-reverse">
                        <path d="M0,0 L8,4 L0,8 z" fill={k === 'hot' ? 'var(--bad)' : 'var(--muted-foreground)'} fillOpacity={k === 'hot' ? 1 : 0.55} />
                      </marker>
                    ))}
                  </defs>
                  {arrows}
                </svg>
              )}
              {showToday && <div aria-hidden className="pointer-events-none absolute inset-y-0 z-10 border-l-2 border-primary" style={{ left: LABEL + x(now) + px / 2 }} />}
            </div>
          </div>
        </div>
      )}
      {unscheduled.length > 0 && (
        <Section id="g-unscheduled" title={t('timeline.unscheduled')} count={unscheduled.length}>
          <ul className="divide-y">{unscheduled.map((u) => (
            <li key={u.id}><button type="button" className="flex min-h-(--row-min) w-full flex-wrap items-center gap-x-3 gap-y-1 px-5 py-(--cell-py) text-left text-sm hover:bg-muted" onClick={() => openPanel(u.type, u.id)}>
              <span className="w-24 shrink-0 text-xs/[18px] text-muted-foreground">{t(`itemType.${u.type}`)}</span>
              <span className="inline-flex shrink-0 items-center gap-1.5"><AccentDot id={u.projectId} /><Key>{u.project}</Key></span>
              <Key>{u.key}</Key><span className="min-w-0 flex-1 truncate">{u.name}</span>
            </button></li>))}</ul>
        </Section>
      )}
      {move && <MoveDialog move={move} projectComplete={move.projectComplete} onClose={(ok) => { setMove(null); if (ok) q.refetch() }} />}
    </Page>
  )
}
