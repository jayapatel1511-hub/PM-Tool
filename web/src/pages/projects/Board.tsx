import { KeyboardSensor, MouseSensor, TouchSensor, useSensor, useSensors, type Announcements } from '@dnd-kit/core'
import { arrayMove, sortableKeyboardCoordinates } from '@dnd-kit/sortable'
import { useQuery } from '@tanstack/react-query'
import { CalendarDays, GitBranch, MessageSquare, Plus } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { toast } from 'sonner'
import { AccentDot, ErrorBanner, Field, Page, selectCls } from '@/components/hub/common'
import { useItemPanel } from '@/components/hub/panel-host'
import { Avatar } from '@/components/hub/people'
import { Chip, Key, PriorityBadge, StatusPill } from '@/components/hub/pills'
import { KanbanBoard, KanbanCard, KanbanCards, KanbanHeader, KanbanProvider } from '@/components/kibo-ui/kanban'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Skeleton } from '@/components/ui/skeleton'
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
    const out = [...m.values()].sort((a, b) => a.label.localeCompare(b.label))
    return out.length ? out : [{ id: 'all', label: '', rows }] // no cards: the lanes still show, each saying it is empty
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
        <Field label={t('board.swimlanes')} htmlFor="board-swim" className="w-full sm:w-56">
          <select id="board-swim" className={selectCls} value={swim} onChange={(e) => set('swim', e.target.value === 'none' ? null : e.target.value)}>
            {SWIMLANES.map((x) => <option key={x} value={x}>{t(`board.swim.${x}`)}</option>)}
          </select>
        </Field>
        <Field label={t('board.sort')} htmlFor="board-sort" className="w-full sm:w-44">
          <select id="board-sort" className={selectCls} value={sort} onChange={(e) => set('sort', e.target.value === 'dueDate' ? null : e.target.value)}>
            <option value="dueDate">{t('board.sortDue')}</option><option value="priority">{t('board.sortPriority')}</option><option value="board">{t('board.sortManual')}</option>
          </select>
        </Field>
        <label className="flex min-h-(--control-h) items-center gap-2 text-sm"><Checkbox checked={side} onCheckedChange={(c) => set('side', c === true ? '1' : null)} />{t('board.showSide')}</label>
      </>} />
      {q.data && q.data.total > MAX_CARDS && <p role="status" className="rounded-md border border-warn/40 bg-warn-bg px-4 py-3 text-sm text-warn"><span aria-hidden>▲ </span>{t('board.tooMany', { n: q.data.total, max: MAX_CARDS })}</p>}
      {hidden > 0 && !side && <p className="text-sm text-muted-foreground">{t('board.hiddenOnHold', { n: hidden })}</p>}
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      {q.isPending ? <BoardSkeleton lanes={lanes.length} /> : (
        // Lanes keep their 248 px width and the board scrolls sideways inside its own region (design §5).
        <div className="scroll-region overflow-x-auto pb-3">
          <div className="w-max min-w-full space-y-6">
            {groups.map((g) => (
              <section key={g.id} aria-label={g.label || t('ptab.board')} className="space-y-2">
                {g.label && <h3 className="sticky left-0 flex w-fit items-center gap-2 text-base/6 font-semibold">{g.label}
                  <span className="rounded-md bg-secondary px-2 py-0.5 text-xs font-medium text-muted-foreground tabular-nums">{g.rows.length}</span></h3>}
                <Swimlane rows={g.rows} columns={columns} onDrop={onDrop} onReorder={onReorder} onAdd={p.permissions.createTaskIn.length > 0 ? () => setCreating(true) : undefined}
                  render={(r) => <CardBody r={r} open={() => openPanel('Task', r.id)} openDeliverable={(id) => openPanel('Deliverable', id)} />} />
              </section>
            ))}
          </div>
        </div>
      )}
      {dialog}
      {creating && <CreateTask p={p} deliverables={deliverables} milestones={milestones} defaults={{ deliverableId: filters.deliverableId, projectDisciplineId: filters.disciplineId }}
        onClose={(id) => { setCreating(false); if (id) { reload(); openPanel('Task', id) } }} />}
      {actions.dialogs}
    </Page>
  )
}

/** Skeleton lanes keep the board's shape while it loads. */
function BoardSkeleton({ lanes }: { lanes: number }) {
  return (
    <div role="status" aria-label={t('app.loading')} className="flex gap-3 overflow-hidden">
      {Array.from({ length: lanes }, (_, i) => (
        <div key={i} className="w-[248px] shrink-0 space-y-2 rounded-lg border bg-muted p-2">
          <Skeleton className="h-7 w-28" />
          {Array.from({ length: 3 }, (_, j) => <Skeleton key={j} className="h-24 w-full rounded-lg" />)}
        </div>
      ))}
    </div>
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
      <DialogContent className="sm:max-w-sm">
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
    <KanbanProvider columns={columns} data={data} onDataChange={setData} sensors={sensors} accessibility={{ announcements }}
      onDragEnd={(e) => {
        const c = data.find((x) => x.id === e.active.id)
        if (!c) return
        if (c.column !== c.r.lane) { onDrop(c.r, c.column, () => setData(make())); return }
        const from = data.findIndex((x) => x.id === e.active.id), to = data.findIndex((x) => x.id === e.over?.id)
        if (from === to || (to < 0 && e.over?.id !== c.column)) return // dropped in place or outside the lane
        const next = to >= 0 ? arrayMove(data, from, to) : [...data.filter((x) => x.id !== c.id), c] // on the lane itself: to its end
        onReorder(next.filter((x) => x.column === c.column).map((x) => x.id), () => setData(make()))
      }}>
      {(col) => {
        const count = data.filter((c) => c.column === col.id).length
        return (
          <KanbanBoard key={col.id} id={col.id} className="max-h-[70dvh]">
            <KanbanHeader>
              <span className="flex items-center gap-2">{col.name}<span className="rounded-md bg-card px-2 py-0.5 text-xs font-medium text-muted-foreground tabular-nums">{count}</span></span>
              {col.id === 'To Do' && onAdd && <Button variant="ghost" size="icon-sm" aria-label={t('task.new')} onClick={onAdd}><Plus className="size-4" /></Button>}
            </KanbanHeader>
            <KanbanCards<Card> id={col.id}>
              {(c) => <KanbanCard key={c.id} id={c.id} name={c.name} column={c.column} handleLabel={t('board.moveCard', { key: c.r.key, name: c.name })} className="select-none">{render(c.r)}</KanbanCard>}
            </KanbanCards>
            {count === 0 && <p className="px-3 pb-4 text-center text-sm text-muted-foreground">{t('board.emptyLane')}</p>}
          </KanbanBoard>
        )
      }}
    </KanbanProvider>
  )
}

/** Card: key, name, exact review status, indicator chips, deliverable, assignee, due date and dependency/comment marks
 *  (§13.4); on a cross-project board also an unambiguous project label (FR-VIS-04). The first row leaves room for the
 *  drag handle in the card's corner. */
export function CardBody({ r, open, openDeliverable, showProject }: { r: TaskRow; open: () => void; openDeliverable: (id: string) => void; showProject?: boolean }) {
  const s = r.state
  const chips = [
    r.lane === 'Review' && <StatusPill key="status" status={r.status} />,
    s?.isOverdue && <Chip key="overdue" tone="bad">{t('ind.overdueD', { n: s.daysOverdue })}</Chip>,
    s?.isDueSoon && !s.isOverdue && <Chip key="soon" tone="warn">{t('ind.dueSoon')}</Chip>,
    s?.isBlocked && <Chip key="blocked" tone="bad">{s.daysBlocked > 0 ? t('ind.blockedD', { n: s.daysBlocked }) : t('ind.blocked')}</Chip>,
    s?.isWaiting && !s.isBlocked && <Chip key="waiting" tone="warn">{t('ind.waiting')}</Chip>,
    s?.isStale && <Chip key="stale" tone="idle">{t('ind.staleD', { n: s.staleDays })}</Chip>,
    r.reviewRound > 0 && <Chip key="round" tone="work">{t('ind.reviewRound', { n: r.reviewRound })}</Chip>,
  ].filter(Boolean)
  const assignee = r.assigneeName && (r.assigneeActive ? r.assigneeName : t('common.inactiveSuffix', { name: r.assigneeName }))
  return (
    <div className="space-y-2 text-sm">
      {showProject && (
        <div className="flex min-w-0 items-center gap-1.5 pr-7 text-xs/[18px] text-muted-foreground" title={`${r.projectNumber} ${r.projectName}`}>
          <AccentDot id={r.projectId} /><span className="key shrink-0">{r.projectNumber}</span><span className="truncate">{r.projectName}</span>
        </div>
      )}
      <div className={cn('flex min-h-6 items-center gap-2', !showProject && 'pr-7')}><Key>{r.key}</Key>{(r.priority === 'High' || r.priority === 'Critical') && <PriorityBadge priority={r.priority} />}</div>
      <button type="button" className="block w-full break-words text-left font-semibold leading-snug hover:underline" onClick={open}>{r.name}</button>
      {(chips.length > 0 || r.deliverableId) && (
        <div className="flex flex-wrap items-center gap-1">
          {chips}
          {r.deliverableId && <button type="button" className="key rounded-md border bg-muted px-1.5 py-px hover:bg-accent" title={r.deliverableName} onClick={() => openDeliverable(r.deliverableId!)}>{r.deliverableKey}</button>}
        </div>
      )}
      <div className="flex items-center gap-2 border-t pt-2 text-xs/[18px] text-muted-foreground">
        {assignee
          ? <><Avatar id={r.assigneeId} name={r.assigneeName} className="size-6" /><span className={cn('min-w-0 flex-1 truncate', r.assigneeActive && 'text-foreground')} title={assignee}>{assignee}</span></>
          : <span className="flex-1 text-warn"><span aria-hidden>▲ </span>{t('ind.unassigned')}</span>}
        {r.dueDate && (
          <span className={cn('inline-flex shrink-0 items-center gap-1 tabular-nums', s?.isOverdue ? 'font-semibold text-bad' : s?.isDueSoon && 'text-warn')}>
            <CalendarDays className="size-3.5" aria-hidden /><span className="sr-only">{t('common.due')} </span>{shortDate(r.dueDate)}
          </span>
        )}
        {r.predecessorCount + r.successorCount > 0 && <GitBranch className="size-3.5 shrink-0" role="img" aria-label={t('board.hasDependencies')} />}
        {r.commentCount > 0 && <span className="inline-flex shrink-0 items-center gap-0.5 tabular-nums" title={t('common.comments')}><MessageSquare className="size-3.5" aria-hidden /><span className="sr-only">{t('common.comments')} </span>{r.commentCount}</span>}
      </div>
    </div>
  )
}
