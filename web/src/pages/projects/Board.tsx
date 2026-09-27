import { KeyboardSensor, MouseSensor, TouchSensor, useSensor, useSensors, type Announcements } from '@dnd-kit/core'
import { arrayMove, sortableKeyboardCoordinates } from '@dnd-kit/sortable'
import { useQuery } from '@tanstack/react-query'
import { Clock, GitBranch, Hourglass, Link2Off, MessageSquare, Plus } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { toast } from 'sonner'
import { ErrorBanner, Loading, Page } from '@/components/hub/common'
import { useItemPanel } from '@/components/hub/panel-host'
import { Avatar } from '@/components/hub/people'
import { Key, PriorityBadge, StatusPill } from '@/components/hub/pills'
import { KanbanBoard, KanbanCard, KanbanCards, KanbanHeader, KanbanProvider } from '@/components/kibo-ui/kanban'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { useProjectRefresh } from '@/hooks/data'
import { ViewMenu } from '@/components/hub/views'
import { get, put, qs } from '@/lib/api'
import { addDays, shortDate, today } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import type { Page as PageOf } from '@/lib/types'
import { cn } from '@/lib/utils'
import { useCurrentProject } from './ProjectLayout'
import { CreateTask, TaskFilterBar, ViewSwitch, errorText, moveFrom, useProjectLists, useTaskActions, useTaskFilters, type TaskRow, type Transition } from './Tasks'

// Kanban board (§13.4, FR-VIS-04): four lanes that present canonical statuses; every drop is a guarded transition.
export const LANES = ['To Do', 'In Progress', 'Review', 'Done']
export const SIDE = ['On Hold', 'Cancelled']
const TARGETS: Record<string, string[]> = {
  'To Do': ['Not Started'], 'In Progress': ['In Progress'], Review: ['Ready for Review', 'In Review', 'Revision Required'], Done: ['Complete'],
  'On Hold': ['On Hold'], Cancelled: ['Cancelled'],
}
export const MAX_CARDS = 500
const SWIMLANES = ['none', 'discipline', 'deliverable', 'assignee']

type Card = { id: string; name: string; column: string; r: TaskRow }

export async function fetchUpTo(path: string, params: Record<string, unknown>, max: number) {
  const items: TaskRow[] = []
  let total = 0
  for (let page = 1; ; page++) {
    const r = await get<PageOf<TaskRow>>(`${path}${qs({ ...params, page, pageSize: 200 })}`)
    items.push(...r.items)
    total = r.totalCount
    if (r.items.length === 0 || items.length >= Math.min(total, max)) break
  }
  return { items: items.slice(0, max), total }
}

export function BoardTab() {
  const p = useCurrentProject()
  const openPanel = useItemPanel()
  const refresh = useProjectRefresh()
  const { sp, set, filters } = useTaskFilters()
  const { deliverables, milestones } = useProjectLists(p.id)
  const side = sp.get('side') === '1'
  const swim = sp.get('swim') ?? 'none'
  const sort = sp.get('sort') === 'priority' ? 'priority' : sp.get('sort') === 'board' ? 'board' : 'dueDate'
  const params = { ...filters, sort, includeCancelled: side, doneSince: addDays(today(), -14) }
  const q = useQuery({ queryKey: ['p', p.id, 'board', params], queryFn: () => fetchUpTo(`projects/${p.id}/tasks`, params, MAX_CARDS) })
  const reload = () => { q.refetch(); refresh(p.id) }
  const actions = useTaskActions(reload)
  const { onDrop, dialog } = useLaneDrop(actions)
  const [creating, setCreating] = useState(false)
  const lanes = side ? [...LANES, ...SIDE] : LANES
  const columns = lanes.map((l) => ({ id: l, name: t(`lane.${l}`) }))
  const all = q.data?.items ?? []
  const rows = all.filter((r) => lanes.includes(r.lane))
  const hidden = all.length - rows.length

  const groups = useMemo(() => {
    if (swim === 'none') return [{ id: 'all', label: '', rows }]
    const key = (r: TaskRow): [string, string] => swim === 'discipline' ? [r.projectDisciplineId, r.disciplineName]
      : swim === 'deliverable' ? [r.deliverableId ?? '', r.deliverableKey ? `${r.deliverableKey} ${r.deliverableName}` : t('task.noDeliverable')]
      : [r.assigneeId ?? '', r.assigneeName ?? t('ind.unassigned')]
    const m = new Map<string, { id: string; label: string; rows: TaskRow[] }>()
    for (const r of rows) { const [id, label] = key(r); const g = m.get(id) ?? { id, label, rows: [] }; g.rows.push(r); m.set(id, g) }
    return [...m.values()].sort((a, b) => a.label.localeCompare(b.label))
  }, [rows, swim])

  /** FR-004: with Manual order chosen, a card dropped within its lane is saved at its new place for the whole team. */
  const onReorder = async (ids: string[], snapBack: () => void) => {
    if (sort !== 'board') { snapBack(); toast.info(t('board.manualHint')); return }
    try { await put(`projects/${p.id}/board-order`, { taskIds: ids }); q.refetch() } catch (e) { snapBack(); toast.error(errorText(e)) }
  }

  return (
    <Page title={t('ptab.board')} subtitle={q.data ? t('board.count', { n: rows.length }) : undefined}
      actions={<><ViewMenu listType="board" projectId={p.id} /><ViewSwitch view="board" />{p.permissions.createTaskIn.length > 0 && <Button onClick={() => setCreating(true)}><Plus className="size-4" />{t('task.new')}</Button>}</>}>
      <TaskFilterBar p={p} deliverables={deliverables} extra={<>
        <select className="h-8 rounded-md border bg-card px-2 text-sm" value={swim} onChange={(e) => set('swim', e.target.value === 'none' ? null : e.target.value)} aria-label={t('board.swimlanes')}>
          {SWIMLANES.map((x) => <option key={x} value={x}>{t(`board.swim.${x}`)}</option>)}
        </select>
        <select className="h-8 rounded-md border bg-card px-2 text-sm" value={sort} onChange={(e) => set('sort', e.target.value === 'dueDate' ? null : e.target.value)} aria-label={t('board.sort')}>
          <option value="dueDate">{t('board.sortDue')}</option><option value="priority">{t('board.sortPriority')}</option><option value="board">{t('board.sortManual')}</option>
        </select>
        <label className="flex items-center gap-1.5 text-sm"><input type="checkbox" checked={side} onChange={(e) => set('side', e.target.checked ? '1' : null)} />{t('board.showSide')}</label>
      </>} />
      {q.data && q.data.total > MAX_CARDS && <div role="status" className="rounded-md border border-warn/30 bg-warn-bg px-3 py-2 text-sm text-warn">{t('board.tooMany', { n: q.data.total, max: MAX_CARDS })}</div>}
      {hidden > 0 && !side && <p className="text-xs text-muted-foreground">{t('board.hiddenOnHold', { n: hidden })}</p>}
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      {q.isPending ? <Loading rows={6} /> : (
        <div className="space-y-4 overflow-x-auto pb-2">
          {groups.map((g) => (
            <section key={g.id} aria-label={g.label || t('ptab.board')}>
              {g.label && <h2 className="mb-1.5 text-sm font-semibold">{g.label} <span className="text-xs font-normal text-muted-foreground">{g.rows.length}</span></h2>}
              <Swimlane rows={g.rows} columns={columns} onDrop={onDrop} onReorder={onReorder} onAdd={p.permissions.createTaskIn.length > 0 ? () => setCreating(true) : undefined}
                render={(r) => <CardBody r={r} open={() => openPanel('Task', r.id)} openDeliverable={(id) => openPanel('Deliverable', id)} />} />
            </section>
          ))}
        </div>
      )}
      {dialog}
      {creating && <CreateTask p={p} deliverables={deliverables} milestones={milestones} defaults={{ deliverableId: filters.deliverableId, projectDisciplineId: filters.disciplineId }}
        onClose={(id) => { setCreating(false); if (id) { reload(); openPanel('Task', id) } }} />}
      {actions.dialogs}
    </Page>
  )
}

/** A move into a lane asks the server which transitions this user may take there (§36.3): refusals explain why and snap
 *  back, several possible statuses ask which one, and review guards and reason dialogs still apply. Board drops and My
 *  Work's completion checkbox both use it, so neither can skip a review. */
export function useLaneDrop(actions: ReturnType<typeof useTaskActions>) {
  const [choose, setChoose] = useState<{ detail: any; options: Transition[] } | null>(null)
  const onDrop = async (r: TaskRow, lane: string, snapBack: () => void) => {
    try {
      const detail = await get(`tasks/${r.id}`)
      const options = (detail.permissions.transitions as Transition[]).filter((x) => (TARGETS[lane] ?? []).includes(x.to))
      const allowed = options.filter((x) => x.allowed)
      if (allowed.length === 0) {
        snapBack()
        const review = lane === 'Done' && r.requiresReview ? t('board.needsReview', { key: r.key, reviewer: r.reviewerName ?? t('ind.noReviewer') }) : null
        toast.error(options.find((x) => x.reason)?.reason ?? (lane === 'Done' ? detail.permissions.completeHint : null) ?? review ?? t('board.notAllowed', { key: r.key, lane: t(`lane.${lane}`) }))
        return
      }
      if (allowed.length > 1) { snapBack(); setChoose({ detail, options: allowed }); return }
      const done = await actions.move(moveFrom(detail, allowed[0]))
      if (done !== true) snapBack()
    } catch (e) { snapBack(); toast.error(errorText(e)) }
  }
  const dialog = choose && (
    <Dialog open onOpenChange={(o) => !o && setChoose(null)}>
      <DialogContent className="max-w-sm">
        <DialogHeader><DialogTitle>{t('board.chooseTitle', { key: choose.detail.task.key })}</DialogTitle><DialogDescription>{t('board.chooseBody')}</DialogDescription></DialogHeader>
        <div className="grid gap-2">
          {choose.options.map((x) => <Button key={x.to} variant="outline" onClick={() => { const c = choose; setChoose(null); actions.move(moveFrom(c.detail, x)) }}>{tv(x.to)}</Button>)}
          <Button variant="ghost" onClick={() => setChoose(null)}>{t('common.cancel')}</Button>
        </div>
      </DialogContent>
    </Dialog>
  )
  return { onDrop, dialog }
}

export function Swimlane({ rows, columns, onDrop, onReorder, onAdd, render }: {
  rows: TaskRow[]; columns: { id: string; name: string }[]; onDrop: (r: TaskRow, lane: string, snapBack: () => void) => void
  onReorder: (ids: string[], snapBack: () => void) => void; onAdd?: () => void; render: (r: TaskRow) => React.ReactNode
}) {
  const make = () => rows.map((r): Card => ({ id: r.id, name: r.name, column: r.lane, r }))
  const [data, setData] = useState<Card[]>(make)
  useEffect(() => setData(make()), [rows]) // eslint-disable-line react-hooks/exhaustive-deps
  const sensors = useSensors(
    useSensor(MouseSensor, { activationConstraint: { distance: 6 } }), // a click opens the card; a drag moves it
    useSensor(TouchSensor, { activationConstraint: { delay: 200, tolerance: 6 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  )
  const name = (id: unknown) => data.find((c) => c.id === id)?.name ?? ''
  const lane = (id: unknown) => columns.find((c) => c.id === id)?.name ?? columns.find((c) => c.id === data.find((x) => x.id === id)?.column)?.name ?? ''
  const announcements: Announcements = {
    onDragStart: ({ active }) => t('board.aPick', { name: name(active.id) }),
    onDragOver: ({ active, over }) => t('board.aOver', { name: name(active.id), lane: lane(over?.id) }),
    onDragEnd: ({ active, over }) => t('board.aDrop', { name: name(active.id), lane: lane(over?.id) }),
    onDragCancel: ({ active }) => t('board.aCancel', { name: name(active.id) }),
  }
  return (
    <KanbanProvider columns={columns} data={data} onDataChange={setData} sensors={sensors} accessibility={{ announcements }} className="min-w-[56rem]"
      onDragEnd={(e) => {
        const c = data.find((x) => x.id === e.active.id)
        if (!c) return
        if (c.column !== c.r.lane) { onDrop(c.r, c.column, () => setData(make())); return }
        const from = data.findIndex((x) => x.id === e.active.id), to = data.findIndex((x) => x.id === e.over?.id)
        if (from === to || (to < 0 && e.over?.id !== c.column)) return // dropped in place or outside the lane
        const next = to >= 0 ? arrayMove(data, from, to) : [...data.filter((x) => x.id !== c.id), c] // on the lane itself: to its end
        onReorder(next.filter((x) => x.column === c.column).map((x) => x.id), () => setData(make()))
      }}>
      {(col) => (
        <KanbanBoard key={col.id} id={col.id} className="max-h-[70dvh] bg-muted/40">
          <KanbanHeader className="flex items-center justify-between">
            <span>{col.name} <span className="text-xs font-normal text-muted-foreground">{data.filter((c) => c.column === col.id).length}</span></span>
            {col.id === 'To Do' && onAdd && <button className="rounded p-0.5 text-muted-foreground hover:bg-muted hover:text-foreground" aria-label={t('task.new')} onClick={onAdd}><Plus className="size-4" /></button>}
          </KanbanHeader>
          <KanbanCards<Card> id={col.id}>
            {(c) => <KanbanCard key={c.id} id={c.id} name={c.name} column={c.column} handleLabel={t('board.moveCard', { key: c.r.key, name: c.name })} className="select-none gap-0 p-2.5 pl-5">{render(c.r)}</KanbanCard>}
          </KanbanCards>
        </KanbanBoard>
      )}
    </KanbanProvider>
  )
}

/** Card: key, name, exact review status, deliverable, assignee, due date and indicator icons (§13.4); on a
 *  cross-project board also an unambiguous project label (FR-VIS-04). */
export function CardBody({ r, open, openDeliverable, showProject }: { r: TaskRow; open: () => void; openDeliverable: (id: string) => void; showProject?: boolean }) {
  const s = r.state
  return (
    <div className="space-y-1.5 text-[13px]">
      {showProject && <div className="truncate text-[11px] text-muted-foreground" title={`${r.projectNumber} ${r.projectName}`}><span className="key font-mono">{r.projectNumber}</span> {r.projectName}</div>}
      <div className="flex items-center justify-between gap-2"><Key>{r.key}</Key>{(r.priority === 'High' || r.priority === 'Critical') && <PriorityBadge priority={r.priority} />}</div>
      <button className="block w-full text-left font-medium leading-snug hover:underline" onClick={open}>{r.name}</button>
      <div className="flex flex-wrap items-center gap-1.5">
        {r.lane === 'Review' && <StatusPill status={r.status} />}
        {r.deliverableId && <button className="rounded border bg-muted px-1.5 text-[11px] hover:bg-accent" title={r.deliverableName} onClick={() => openDeliverable(r.deliverableId!)}>{r.deliverableKey}</button>}
      </div>
      <div className="flex items-center gap-2 text-xs">
        {r.assigneeName ? <Avatar name={r.assigneeName} title={r.assigneeName} className={cn(!r.assigneeActive && 'opacity-50')} /> : <span className="text-warn">{t('ind.unassigned')}</span>}
        {r.dueDate && <span className={cn(s?.isOverdue ? 'font-medium text-bad' : s?.isDueSoon ? 'text-warn' : 'text-muted-foreground')}>{s?.isOverdue ? t('ind.overdueD', { n: s.daysOverdue }) : shortDate(r.dueDate)}</span>}
        <span className="ml-auto flex items-center gap-1.5 text-muted-foreground">
          {s?.isBlocked && <Link2Off className="size-3.5 text-bad" role="img" aria-label={t('ind.blocked')} />}
          {s?.isWaiting && !s.isBlocked && <Hourglass className="size-3.5 text-warn" role="img" aria-label={t('ind.waiting')} />}
          {s?.isStale && <Clock className="size-3.5" role="img" aria-label={t('ind.stale')} />}
          {r.predecessorCount + r.successorCount > 0 && <GitBranch className="size-3.5" role="img" aria-label={t('board.hasDependencies')} />}
          {r.commentCount > 0 && <span className="inline-flex items-center gap-0.5" title={t('common.comments')}><MessageSquare className="size-3.5" aria-hidden />{r.commentCount}</span>}
        </span>
      </div>
    </div>
  )
}
