import { useQuery } from '@tanstack/react-query'
import { ChevronDown, ChevronLeft, ChevronRight, ChevronsDownUp, ChevronsUpDown } from 'lucide-react'
import { useRef, useState, type PointerEvent as ReactPointerEvent } from 'react'
import { Link, useSearchParams } from 'react-router'
import { Empty, ErrorBanner, Loading, Page } from '@/components/hub/common'
import { useItemPanel } from '@/components/hub/panel-host'
import { Key } from '@/components/hub/pills'
import { useScope, WorkspaceTabs } from '@/components/hub/workspace'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { get, qs } from '@/lib/api'
import { addDays, daysBetween, fmtDate, monthLabel, shortDate, today } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import { cn } from '@/lib/utils'
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
const H = { project: 30, deliverable: 28, task: 24 } as const
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

  if (!scope.ready || q.isPending) return <Page title={t('wsview.timeline')}><WorkspaceTabs /><Loading rows={8} /></Page>
  if (q.error) return <Page title={t('wsview.timeline')}><WorkspaceTabs /><ErrorBanner error={q.error} retry={() => q.refetch()} /></Page>

  const spanOf = (i: Dated): Span => {
    const due = i.dueDate!
    const b = i.startDate ?? i.createdOn
    const ghost = i.originalDueDate && (i.originalDueDate !== i.dueDate || (i.originalStartDate ?? null) !== (i.startDate ?? null)) ? { start: i.originalStartDate ?? i.originalDueDate, end: i.originalDueDate } : null
    return { start: b > due ? due : b, end: due, late: i.isOverdue && due < now, ghost }
  }
  const rows: Row[] = []
  const unscheduled: { type: 'Task' | 'Deliverable'; id: string; key: string; name: string; project: string }[] = []
  for (const g of q.data.projects) {
    const tasks = g.data.tasks.filter((tk) => !hide || tk.status !== 'Complete')
    const dels = g.data.deliverables.filter((d) => !hide || !CLOSED.includes(d.status))
    unscheduled.push(...dels.filter((d) => !d.dueDate).map((d) => ({ type: 'Deliverable' as const, id: d.id, key: d.key, name: d.name, project: g.project.projectNumber })),
      ...tasks.filter((tk) => !tk.dueDate).map((tk) => ({ type: 'Task' as const, id: tk.id, key: tk.key, name: tk.name, project: g.project.projectNumber })))
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

  const ticks: { date: string; label?: string; major: boolean }[] = []
  for (let i = 0; i < LEN[zoom]; i++) {
    const d = addDays(start, i)
    const dow = new Date(d + 'T00:00:00Z').getUTCDay()
    const first = d.endsWith('-01')
    if (zoom === 'week') ticks.push({ date: d, label: dow === 1 ? shortDate(d) : undefined, major: dow === 1 })
    else if (zoom === 'month' && (dow === 1 || first)) ticks.push({ date: d, label: first ? monthLabel(d) : undefined, major: first })
    else if (zoom === 'quarter' && first) ticks.push({ date: d, label: monthLabel(d), major: ['01', '04', '07', '10'].includes(d.slice(5, 7)) })
  }

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
    const thin = r.kind === 'task'
    const left = x(span.start), right = x(span.end) + px
    if (right < 0 || left > width) return null
    const dx = shift?.id === item.id ? shift.days * px : 0
    const type = r.kind === 'task' ? 'Task' : 'Deliverable'
    const label = t('timeline.barLabel', { key: item.key, name: item.name, from: fmtDate(span.start), to: fmtDate(span.end), status: tv(item.status), pct: item.progressPct })
    const drop = (n: number) => setMove({ kind: r.kind, id: item.id, key: item.key, name: item.name, start: item.startDate, due: item.dueDate!, days: n, rowVersion: item.rowVersion,
      needsReason: r.kind === 'task' && (item as GTask).dueNeedsReason, projectComplete: r.g.data.permissions.needsReason })
    const props = dragProps(item.id, item.canEditDates, drop, () => openPanel(type, item.id))
    const top = thin ? 7 : 5, height = thin ? 10 : 18
    return (
      <>
        {span.ghost && <span aria-hidden className="absolute rounded-sm border border-dashed border-foreground/40" title={t('timeline.baseline', { from: fmtDate(span.ghost.start), to: fmtDate(span.ghost.end) })}
          style={{ left: x(span.ghost.start), width: Math.max((daysBetween(span.ghost.start, span.ghost.end) + 1) * px, 4), top: top - 3, height: height + 6 }} />}
        <button type="button" {...props} aria-label={label} title={label + (item.canEditDates ? ` · ${t('timeline.dragHint')}` : '')}
          className={cn('absolute overflow-hidden rounded-sm border text-left focus-visible:ring-2', dx && 'ring-2 ring-primary')}
          style={{ left: left + dx, width: Math.max(right - left, 4), top, height, borderColor: tone(item.status), background: toneBg(item.status), touchAction: item.canEditDates ? 'none' : undefined, cursor: item.canEditDates ? 'grab' : 'pointer' }}>
          <span className="absolute inset-y-0 left-0 opacity-40" style={{ width: `${item.progressPct}%`, background: tone(item.status) }} />
          {!thin && right - left > 44 && <span className="relative px-1 text-[10px] leading-4">{item.progressPct}%</span>}
        </button>
        {dx !== 0 && <span className="absolute z-30 rounded bg-primary px-1 text-[10px] text-primary-foreground" style={{ left: right + dx + 4, top }}>{shift!.days > 0 ? '+' : ''}{shift!.days} d</span>}
        {span.late && <span aria-hidden className="absolute rounded-r-sm border border-l-0 border-bad" title={t('timeline.overdueBy', { n: daysBetween(span.end, now) })}
          style={{ left: right, width: Math.max(daysBetween(span.end, now) * px, 2), top, height, background: HATCH }} />}
      </>
    )
  }

  const expandable = q.data.projects.map((g) => g.project.id)
  const allOpen = closed.size === 0
  const move2 = (n: number) => set('start', addDays(start, n))
  return (
    <Page title={t('wsview.timeline')} subtitle={t('gantt.range', { from: fmtDate(start), to: fmtDate(end) })}>
      <WorkspaceTabs />
      <div className="no-print flex flex-wrap items-center gap-2">
        <div className="inline-flex overflow-hidden rounded-md border bg-card" role="group" aria-label={t('timeline.zoom')}>
          {(Object.keys(ZOOM) as Zoom[]).map((z) => (
            <button key={z} type="button" aria-pressed={zoom === z} onClick={() => set('zoom', z === 'month' ? null : z)}
              className={cn('h-8 px-3 text-sm', zoom === z ? 'bg-accent font-medium' : 'hover:bg-muted')}>{t(`timeline.zoom.${z}`)}</button>
          ))}
        </div>
        <div className="inline-flex items-center gap-1" role="group" aria-label={t('gantt.navigate')}>
          <Button variant="outline" size="sm" className="size-8 p-0" aria-label={t('common.previous')} onClick={() => move2(-Math.round(LEN[zoom] / 2))}><ChevronLeft className="size-4" /></Button>
          <Button variant="outline" size="sm" onClick={() => set('start', null)}>{t('common.today')}</Button>
          <Button variant="outline" size="sm" className="size-8 p-0" aria-label={t('common.next')} onClick={() => move2(Math.round(LEN[zoom] / 2))}><ChevronRight className="size-4" /></Button>
        </div>
        <span className="text-sm tabular-nums text-muted-foreground" aria-live="polite">{fmtDate(start)} – {fmtDate(end)}</span>
        <label className="flex items-center gap-2 text-sm"><Checkbox checked={hide} onCheckedChange={(c) => set('hideCompleted', c ? 'true' : null)} />{t('timeline.hideCompleted')}</label>
        {expandable.length > 0 && <Button variant="ghost" size="sm" onClick={() => setClosed(allOpen ? new Set(expandable) : new Set())}>
          {allOpen ? <ChevronsDownUp className="size-4" /> : <ChevronsUpDown className="size-4" />}{allOpen ? t('gantt.collapse') : t('gantt.expand')}</Button>}
      </div>
      <Legend />
      {q.data.truncated && <div role="status" className="rounded-md border border-warn/30 bg-warn-bg px-3 py-2 text-sm text-warn">{t('gantt.truncated', { n: q.data.max })}</div>}
      {rows.length === 0 ? <div className="rounded-lg border bg-card"><Empty>{t('gantt.empty')}</Empty></div> : (
        <div className="overflow-x-auto rounded-lg border bg-card" role="region" aria-label={t('wsview.timeline')}>
          <div className="relative" style={{ width: LABEL + width }}>
            <div className="flex h-8 border-b text-[11px] text-muted-foreground">
              <div className="sticky left-0 z-20 flex shrink-0 items-center border-r bg-card px-3 font-medium" style={{ width: LABEL }}>{t('timeline.items')}</div>
              <div className="relative overflow-hidden" style={{ width }} aria-hidden>
                {ticks.map((k) => (
                  <div key={k.date} className={cn('absolute top-0 h-full border-l', k.major ? 'border-border' : 'border-border/40')} style={{ left: x(k.date) }}>
                    {k.label && <span className="absolute left-1 top-1 whitespace-nowrap">{k.label}</span>}
                  </div>
                ))}
              </div>
            </div>
            <div className="relative">
              {rows.map((r) => {
                if (r.kind === 'project') {
                  const p = r.g.project
                  const open = !closed.has(p.id)
                  const ps = p.startDate && p.targetCompletionDate ? { l: Math.max(x(p.startDate), 0), r: Math.min(x(p.targetCompletionDate) + px, width) } : null
                  return (
                    <div key={r.id} className="flex border-b bg-muted/40" style={{ height: H.project }}>
                      <div className="sticky left-0 z-20 flex shrink-0 items-center gap-1 border-r bg-muted px-1.5 text-xs font-semibold" style={{ width: LABEL }}>
                        <button type="button" className="rounded p-0.5 hover:bg-accent" aria-expanded={open} aria-label={t(open ? 'gantt.collapseProject' : 'gantt.expandProject', { name: p.projectNumber })}
                          onClick={() => setClosed((s) => { const n = new Set(s); if (n.has(p.id)) n.delete(p.id); else n.add(p.id); return n })}>
                          {open ? <ChevronDown className="size-3.5" /> : <ChevronRight className="size-3.5" />}
                        </button>
                        <Link to={`/projects/${p.projectNumber}/timeline`} className="flex min-w-0 flex-1 items-center gap-1.5 hover:underline" title={`${p.projectNumber} ${p.name}`}>
                          <span className="key font-mono font-normal text-muted-foreground">{p.projectNumber}</span><span className="truncate">{p.name}</span>
                        </Link>
                        <span className="font-normal text-muted-foreground">{r.count}</span>
                      </div>
                      <div className="relative overflow-hidden" style={{ width }}>
                        {ps && ps.r > ps.l && <span aria-hidden className="absolute top-[13px] h-1 rounded-full bg-foreground/25" style={{ left: ps.l, width: ps.r - ps.l }} title={t('gantt.projectSpan', { from: fmtDate(p.startDate), to: fmtDate(p.targetCompletionDate) })} />}
                        {r.g.data.milestones.filter((m) => m.date >= start && m.date <= end).map((m) => (
                          <button key={m.id} type="button" className="absolute top-[7px] -translate-x-1/2" style={{ left: x(m.date) + px / 2 }} onClick={() => openPanel('Milestone', m.id)}
                            aria-label={t('timeline.milestoneLabel', { key: m.key, name: m.name, date: fmtDate(m.date), status: tv(m.status) })} title={`${m.key} ${m.name} · ${fmtDate(m.date)} · ${tv(m.status)}`}>
                            <span className="block size-3.5 rotate-45 border border-white shadow" style={{ background: tone(m.status) }} />
                          </button>
                        ))}
                      </div>
                    </div>
                  )
                }
                const type = r.kind === 'task' ? 'Task' : 'Deliverable'
                return (
                  <div key={r.id} className={cn('flex border-b', r.kind === 'task' && 'border-border/60')} style={{ height: H[r.kind] }}>
                    <button type="button" className={cn('sticky left-0 z-20 flex shrink-0 items-center gap-2 border-r bg-card pr-3 text-left hover:bg-muted', r.kind === 'task' ? 'text-[11px]' : 'text-xs font-medium', r.indent ? 'pl-9' : 'pl-5')}
                      style={{ width: LABEL }} onClick={() => openPanel(type, r.item.id)} title={`${r.item.key} ${r.item.name}${r.kind === 'task' && (r.item as GTask).assignee ? ` · ${(r.item as GTask).assignee}` : ''}`}>
                      <span className="key shrink-0 text-muted-foreground">{r.item.key.split('-').pop()}</span><span className="truncate">{r.item.name}</span>
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
            </div>
            {now >= start && now <= end && (
              <div aria-hidden className="pointer-events-none absolute bottom-0 top-0 z-10 border-l-2 border-primary" style={{ left: LABEL + x(now) + px / 2 }}>
                <span className="absolute left-1 top-[18px] whitespace-nowrap bg-card/80 px-0.5 text-[10px] font-medium text-primary">{t('common.today')}</span>
              </div>
            )}
          </div>
        </div>
      )}
      {unscheduled.length > 0 && (
        <section className="rounded-lg border bg-card" aria-labelledby="g-unscheduled">
          <h2 id="g-unscheduled" className="border-b px-4 py-2 text-sm font-semibold">{t('timeline.unscheduled')} <span className="text-muted-foreground">{unscheduled.length}</span></h2>
          <ul className="divide-y text-sm">{unscheduled.map((u) => (
            <li key={u.id}><button type="button" className="flex w-full items-center gap-2 px-4 py-1.5 text-left hover:bg-muted/40" onClick={() => openPanel(u.type, u.id)}>
              <span className="w-20 shrink-0 text-xs text-muted-foreground">{t(`itemType.${u.type}`)}</span><span className="key font-mono text-xs text-muted-foreground">{u.project}</span><Key>{u.key}</Key><span className="flex-1 truncate">{u.name}</span>
            </button></li>))}</ul>
        </section>
      )}
      {move && <MoveDialog move={move} projectComplete={move.projectComplete} onClose={(ok) => { setMove(null); if (ok) q.refetch() }} />}
    </Page>
  )
}
