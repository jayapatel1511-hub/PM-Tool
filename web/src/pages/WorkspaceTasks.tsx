import { useQuery } from '@tanstack/react-query'
import { ArrowDown, ArrowUp, Plus } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { AccentDot, ActiveFilters, ChipToggle, Empty, ErrorBanner, Field, FilterBar, Loading, Missing, Notice, Page, TableRegion, selectCls, tdCls, thCls } from '@/components/hub/common'
import { NewTaskDialog } from '@/components/hub/new-item'
import { useItemPanel } from '@/components/hub/panel-host'
import { Avatar, PeoplePicker } from '@/components/hub/people'
import { Key, PriorityBadge } from '@/components/hub/pills'
import { ViewMenu } from '@/components/hub/views'
import { useScope, useScopeProjects, WorkspaceTabs } from '@/components/hub/workspace'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { get, put, qs } from '@/lib/api'
import { useMe } from '@/lib/auth'
import { addDays, fmtDate, today } from '@/lib/format'
import { plural, t, tv } from '@/lib/i18n'
import type { Page as PageOf } from '@/lib/types'
import { cn } from '@/lib/utils'
import { CardBody, LANES, MAX_CARDS, SIDE, Swimlane, fetchUpTo, useLaneDrop } from './projects/Board'
import { PRIORITIES, QUICK, StatusMenu, TASK_STATUSES, TaskIndicators, ViewSwitch, errorText, useTaskActions, type TaskRow } from './projects/Tasks'

const PATHS = { tasks: '/tasks', board: '/boards' }
/** URL parameters that are task filters (everything else is scope, view or layout). */
const FILTERS = ['q', 'status', 'priority', 'assigneeId', 'projectId', 'createdBy', 'dueFrom', 'dueTo', 'open', ...QUICK] as const

function useFilters() {
  const [sp, setSp] = useSearchParams()
  const filters = Object.fromEntries(FILTERS.flatMap((k) => (sp.get(k) ? [[k, sp.get(k)!]] : []))) as Record<string, string>
  const set = (k: string, v?: string | null) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); n.delete('page'); setSp(n, { replace: true }) }
  const clear = () => { const n = new URLSearchParams(sp); for (const k of FILTERS) n.delete(k); n.delete('page'); setSp(n, { replace: true }) }
  return { sp, filters, set, clear }
}

/** Filters shared by the cross-project list and board (FR-VIS-04: switching keeps scope and filters). `rows` name the
 *  people behind an assignee or creator filter. */
function TaskFilters({ rows, extra }: { rows: TaskRow[]; extra?: React.ReactNode }) {
  const scope = useScope()
  const projects = useScopeProjects(scope)
  const { filters: f, set, clear } = useFilters()
  // The box shows the applied search until someone types, so a removed token, Clear or a saved view updates it too.
  const [draft, setDraft] = useState<string | null>(null)
  const term = draft ?? f.q ?? ''
  const statuses = (f.status ?? '').split(',').filter(Boolean)
  const assignee = rows.find((r) => r.assigneeId === f.assigneeId)?.assigneeName
  // Every active filter is a removable token (§13.0), including those that links carry beyond the visible controls; a
  // value that cannot be named here shows as Missing.
  const flags = (['open', ...QUICK] as const).filter((k) => f[k] === 'true')
  const tokens = [
    ...([
      ['q', t('common.search'), f.q && `“${f.q}”`],
      ['projectId', t('common.project'), projects.data?.find((p) => p.id === f.projectId)?.projectNumber],
      ['status', t('common.status'), statuses.map(tv).join(', ')],
      ['priority', t('common.priority'), f.priority && tv(f.priority)],
      ['assigneeId', t('field.AssigneeId'), assignee],
      ['createdBy', t('ws.createdBy'), rows.find((r) => r.createdBy === f.createdBy)?.createdByName],
      ['dueFrom', t('tfilter.dueFrom'), f.dueFrom && fmtDate(f.dueFrom)],
      ['dueTo', t('tfilter.dueTo'), f.dueTo && fmtDate(f.dueTo)],
    ] as const).filter(([k]) => f[k]).map(([key, label, value]) => ({ key, label, value: value || <Missing /> })),
    ...flags.map((k) => ({ key: k, label: k === 'open' ? t('tfilter.open') : t(`tquick.${k}`), value: t('common.yes') })),
  ]
  return (
    <FilterBar>
      <div className="flex flex-wrap items-end gap-3">
        <Field label={t('common.search')} htmlFor="wt-search" className="w-full sm:w-56">
          <form onSubmit={(e) => { e.preventDefault(); set('q', term.trim() || null); setDraft(null) }}>
            <Input id="wt-search" type="search" placeholder={t('task.searchPlaceholder')} value={term}
              onChange={(e) => { if (e.target.value) setDraft(e.target.value); else { setDraft(null); set('q', null) } }} />
          </form>
        </Field>
        <Field label={t('common.project')} htmlFor="wt-project" className="w-full sm:w-56">
          <select id="wt-project" className={selectCls} value={f.projectId ?? ''} onChange={(e) => set('projectId', e.target.value)}>
            <option value="">{t('ws.anyProject')}</option>{projects.data?.map((p) => <option key={p.id} value={p.id}>{p.projectNumber} {p.name}</option>)}
          </select>
        </Field>
        <Field label={t('common.status')} htmlFor="wt-status" className="w-full sm:w-44">
          <select id="wt-status" className={selectCls} value={statuses.length > 1 ? '' : f.status ?? ''} onChange={(e) => set('status', e.target.value)}>
            <option value="">{t('task.anyStatus')}</option>{TASK_STATUSES.map((x) => <option key={x} value={x}>{tv(x)}</option>)}
          </select>
        </Field>
        <Field label={t('common.priority')} htmlFor="wt-priority" className="w-full sm:w-40">
          <select id="wt-priority" className={selectCls} value={f.priority ?? ''} onChange={(e) => set('priority', e.target.value)}>
            <option value="">{t('task.anyPriority')}</option>{PRIORITIES.map((x) => <option key={x} value={x}>{tv(x)}</option>)}
          </select>
        </Field>
        <Field label={t('field.AssigneeId')} htmlFor="wt-assignee" className="w-full sm:w-52">
          <PeoplePicker id="wt-assignee" value={f.assigneeId ?? null} valueName={assignee} onChange={(v) => set('assigneeId', v)} placeholder={t('task.anyAssignee')} label={t('field.AssigneeId')} />
        </Field>
        {extra}
      </div>
      <div className="flex flex-wrap items-center gap-2" role="group" aria-labelledby="wt-quick">
        <span id="wt-quick" className="mr-1 text-sm font-medium">{t('task.quickFilters')}</span>
        {(['open', ...QUICK] as const).map((k) => (
          <ChipToggle key={k} on={f[k] === 'true'} onClick={() => set(k, f[k] === 'true' ? null : 'true')}>{k === 'open' ? t('tfilter.open') : t(`tquick.${k}`)}</ChipToggle>
        ))}
      </div>
      <ActiveFilters tokens={tokens} onRemove={(k) => set(k, null)} onClear={clear} />
    </FilterBar>
  )
}

/** Tasks (§36.1): the selected projects' combined task list, with project labels and guarded status changes. */
export function WorkspaceTasksPage() {
  const scope = useScope()
  const me = useMe()
  const openPanel = useItemPanel()
  const { sp, filters, set, clear } = useFilters()
  const page = Number(sp.get('page') ?? 1)
  const sort = sp.get('sort') ?? undefined
  const params = { projects: scope.api, ...filters, sort, page, pageSize: 100 }
  const q = useQuery({ queryKey: ['wtasks', params], enabled: scope.ready, queryFn: () => get<PageOf<TaskRow>>(`tasks${qs(params)}`) })
  const actions = useTaskActions(() => q.refetch())
  const [creating, setCreating] = useState(false)
  const [field, dir] = (sort ?? 'dueDate:asc').split(':')
  const th = (k: string, label: string, numeric?: boolean) => (
    <th scope="col" className={cn(thCls, numeric && 'text-right')} aria-sort={field === k ? (dir === 'desc' ? 'descending' : 'ascending') : undefined}>
      <button type="button" className="inline-flex items-center gap-1 hover:text-foreground" onClick={() => set('sort', field === k && dir !== 'desc' ? `${k}:desc` : `${k}:asc`)}>
        {t(label)}{field === k && (dir === 'desc' ? <ArrowDown className="size-3.5" /> : <ArrowUp className="size-3.5" />)}
      </button>
    </th>
  )
  const rows = q.data?.items ?? []
  const pages = q.data ? Math.max(1, Math.ceil(q.data.totalCount / q.data.pageSize)) : 1
  const filtered = Object.keys(filters).length > 0
  return (
    <Page title={t('nav.tasks')} subtitle={q.data ? plural(q.data.totalCount, 'ws.oneTask', 'ws.nTasks', { n: q.data.totalCount }) : scope.label}
      actions={<><ViewMenu listType="workspace-tasks" extra={() => scope.params} /><ViewSwitch view="tasks" paths={PATHS} />
        {me.capabilities.createTask && <Button onClick={() => setCreating(true)}><Plus className="size-4" />{t('task.new')}</Button>}</>}>
      <WorkspaceTabs />
      <TaskFilters rows={rows} />
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      {q.isPending ? <div className="rounded-lg border bg-card"><Loading rows={8} /></div> : rows.length === 0 ? (
        <div className="rounded-lg border bg-card"><Empty action={filtered && <Button variant="outline" onClick={clear}>{t('filters.clear')}</Button>}>{t('ws.noTasks')}</Empty></div>
      ) : <>
        {/* Phones get cards (§13.0 Responsive) with the same task, project, status and indicators. */}
        <ul className="space-y-2 md:hidden" aria-label={t('nav.tasks')}>
          {rows.map((r) => (
            <li key={r.id} className="space-y-2 rounded-lg border bg-card p-4 text-sm">
              <p className="flex flex-wrap items-center gap-2" title={r.projectName}><AccentDot id={r.projectId} /><Key>{r.projectNumber}</Key><Key>{r.key}</Key></p>
              <button type="button" className="block text-left font-semibold hover:underline" onClick={() => openPanel('Task', r.id)}>{r.name}</button>
              <div className="flex flex-wrap items-center gap-2"><StatusMenu r={r} onMove={actions.move} /><PriorityBadge priority={r.priority} /><TaskIndicators r={r} /></div>
              <p className="flex flex-wrap gap-x-4 gap-y-1 text-xs/[18px] text-muted-foreground">
                {r.assigneeName ?? <span className="text-warn">{t('ind.unassigned')}</span>}
                <span className={cn('tabular-nums', r.state?.isOverdue && 'font-medium text-bad')}>{t('col.due')} {fmtDate(r.dueDate)}</span>
                <span className="tabular-nums">{t('col.progress')} {r.progressPct}%</span>
              </p>
            </li>
          ))}
        </ul>
        <TableRegion className="hidden md:block">
          <table className="w-full text-sm">
            <caption className="sr-only">{t('nav.tasks')}</caption>
            <thead className="bg-muted">
              <tr>{th('key', 'col.key')}{th('name', 'col.name')}<th scope="col" className={thCls}>{t('col.project')}</th>
                {th('status', 'col.status')}<th scope="col" className={thCls}>{t('col.assignee')}</th>{th('dueDate', 'col.due')}{th('priority', 'col.priority')}
                {th('progress', 'col.progress', true)}<th scope="col" className={thCls}><span className="sr-only">{t('col.indicators')}</span></th></tr>
            </thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.id} className="border-t hover:bg-muted">
                  <td className={cn(tdCls, 'whitespace-nowrap')}><button type="button" className="hover:underline" onClick={() => openPanel('Task', r.id)}><Key>{r.key}</Key></button></td>
                  <td className={cn(tdCls, 'min-w-48 max-w-[22rem]')}><button type="button" className="text-left font-medium hover:underline" onClick={() => openPanel('Task', r.id)}>{r.name}</button></td>
                  <td className={cn(tdCls, 'whitespace-nowrap')} title={r.projectName}><span className="inline-flex items-center gap-2"><AccentDot id={r.projectId} /><Key>{r.projectNumber}</Key></span></td>
                  <td className={tdCls}><StatusMenu r={r} onMove={actions.move} /></td>
                  <td className={cn(tdCls, 'whitespace-nowrap')}>{r.assigneeName
                    ? <span className="inline-flex items-center gap-2"><Avatar id={r.assigneeId} name={r.assigneeName} />{r.assigneeName}</span>
                    : <span className="text-warn">{t('ind.unassigned')}</span>}</td>
                  <td className={cn(tdCls, 'whitespace-nowrap tabular-nums', r.state?.isOverdue && 'font-medium text-bad')}>{fmtDate(r.dueDate)}</td>
                  <td className={tdCls}><PriorityBadge priority={r.priority} /></td>
                  <td className={cn(tdCls, 'text-right tabular-nums')}>{r.progressPct}%</td>
                  <td className={tdCls}><TaskIndicators r={r} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </TableRegion>
      </>}
      {pages > 1 && (
        <div className="flex items-center justify-end gap-3">
          <Button variant="outline" disabled={page <= 1} onClick={() => set('page', String(page - 1))}>{t('common.previous')}</Button>
          <span className="text-sm tabular-nums" aria-live="polite">{t('common.pageOf', { page, pages })}</span>
          <Button variant="outline" disabled={page >= pages} onClick={() => set('page', String(page + 1))}>{t('common.next')}</Button>
        </div>
      )}
      {creating && <NewTaskDialog projectId={filters.projectId} onClose={(id) => { setCreating(false); if (id) q.refetch() }} />}
      {actions.dialogs}
    </Page>
  )
}

const SWIM = ['none', 'project', 'assignee']

/** Boards (§36.3, FR-VIS-04): the selected projects' four-lane board with project labels on every card; moves are guarded
 *  transitions, and a named workspace keeps its owner's own card order. */
export function WorkspaceBoardPage() {
  const scope = useScope()
  const me = useMe()
  const openPanel = useItemPanel()
  const { sp, filters, set, clear } = useFilters()
  const side = sp.get('side') === '1'
  const swim = sp.get('swim') ?? 'none'
  const manual = sp.get('sort') === 'board' && !!scope.ws
  const sort = manual ? 'board' : sp.get('sort') === 'priority' ? 'priority' : 'dueDate'
  const params = { projects: scope.api, ...filters, sort, includeCancelled: side, doneSince: addDays(today(), -14), workspaceId: manual ? scope.ws!.id : undefined }
  const q = useQuery({ queryKey: ['wboard', params], enabled: scope.ready, queryFn: () => fetchUpTo('tasks', params, MAX_CARDS) })
  const actions = useTaskActions(() => q.refetch())
  const { onDrop, dialog } = useLaneDrop(actions)
  const [creating, setCreating] = useState(false)
  const lanes = side ? [...LANES, ...SIDE] : LANES
  const columns = lanes.map((l) => ({ id: l, name: t(`lane.${l}`) }))
  const all = q.data?.items ?? []
  const rows = all.filter((r) => lanes.includes(r.lane))
  const groups = useMemo(() => {
    if (swim === 'none') return [{ id: 'all', label: '', rows }]
    const m = new Map<string, { id: string; label: string; rows: TaskRow[] }>()
    for (const r of rows) {
      const [id, label] = swim === 'project' ? [r.projectId, `${r.projectNumber} ${r.projectName}`] : [r.assigneeId ?? '', r.assigneeName ?? t('ind.unassigned')]
      const g = m.get(id) ?? { id, label, rows: [] }; g.rows.push(r); m.set(id, g)
    }
    return [...m.values()].sort((a, b) => a.label.localeCompare(b.label))
  }, [rows, swim])

  const onReorder = async (ids: string[], snapBack: () => void) => {
    if (!manual) { snapBack(); toast.info(scope.ws ? t('board.manualHint') : t('ws.manualNeedsWorkspace')); return }
    try { await put(`workspaces/${scope.ws!.id}/board-order`, { taskIds: ids }); q.refetch() } catch (e) { snapBack(); toast.error(errorText(e)) }
  }
  return (
    <Page title={t('nav.boards')} subtitle={q.data ? t('board.count', { n: rows.length }) : scope.label}
      actions={<><ViewMenu listType="workspace-board" extra={() => scope.params} /><ViewSwitch view="board" paths={PATHS} />
        {me.capabilities.createTask && <Button onClick={() => setCreating(true)}><Plus className="size-4" />{t('task.new')}</Button>}</>}>
      <WorkspaceTabs />
      <TaskFilters rows={all} extra={<>
        <Field label={t('board.swimlanes')} htmlFor="wb-swim" className="w-full sm:w-52">
          <select id="wb-swim" className={selectCls} value={swim} onChange={(e) => set('swim', e.target.value === 'none' ? null : e.target.value)}>
            {SWIM.map((x) => <option key={x} value={x}>{t(`board.swim.${x}`)}</option>)}
          </select>
        </Field>
        <Field label={t('board.sort')} htmlFor="wb-sort" className="w-full sm:w-60">
          <select id="wb-sort" className={selectCls} value={sort} onChange={(e) => set('sort', e.target.value === 'dueDate' ? null : e.target.value)}>
            <option value="dueDate">{t('board.sortDue')}</option><option value="priority">{t('board.sortPriority')}</option>
            <option value="board" disabled={!scope.ws}>{scope.ws ? t('board.sortManual') : t('ws.manualNeedsWorkspaceShort')}</option>
          </select>
        </Field>
        <label className="flex min-h-(--control-h) items-center gap-2 text-sm"><Checkbox checked={side} onCheckedChange={(c) => set('side', c ? '1' : null)} />{t('board.showSide')}</label>
      </>} />
      {q.data && q.data.total > MAX_CARDS && <Notice title={t('ws.truncatedTitle')}>{t('board.tooMany', { n: q.data.total, max: MAX_CARDS })}</Notice>}
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      {q.isPending ? <BoardSkeleton lanes={lanes.length} /> : rows.length === 0 ? (
        <div className="rounded-lg border bg-card"><Empty action={Object.keys(filters).length > 0 && <Button variant="outline" onClick={clear}>{t('filters.clear')}</Button>}>{t('ws.noTasks')}</Empty></div>
      ) : (
        // 248 px lanes (Swimlane) scroll sideways inside their own region, as on the project board (design §5).
        <div className="scroll-region overflow-x-auto pb-3">
          <div className="w-max min-w-full space-y-6">
            {groups.map((g) => (
              <section key={g.id} aria-label={g.label || t('nav.boards')} className="space-y-2">
                {g.label && (
                  <h2 className="sticky left-0 flex w-fit max-w-[calc(100vw-4rem)] items-center gap-2 text-base/6 font-semibold">
                    {swim === 'project' ? <AccentDot id={g.id} /> : g.id && <Avatar id={g.id} name={g.label} />}
                    <span className="truncate">{g.label}</span>
                    <span className="rounded-md bg-secondary px-2 py-0.5 text-xs font-medium text-muted-foreground tabular-nums">{g.rows.length}</span>
                  </h2>
                )}
                <Swimlane rows={g.rows} columns={columns} onDrop={onDrop} onReorder={onReorder} onAdd={me.capabilities.createTask ? () => setCreating(true) : undefined}
                  render={(r) => <CardBody r={r} showProject open={() => openPanel('Task', r.id)} openDeliverable={(id) => openPanel('Deliverable', id)} />} />
              </section>
            ))}
          </div>
        </div>
      )}
      {creating && <NewTaskDialog projectId={filters.projectId} onClose={(id) => { setCreating(false); if (id) q.refetch() }} />}
      {dialog}
      {actions.dialogs}
    </Page>
  )
}

/** Lane-shaped placeholders while the board loads. */
function BoardSkeleton({ lanes }: { lanes: number }) {
  return (
    <div role="status" aria-label={t('app.loading')} className="flex gap-3 overflow-hidden">
      {Array.from({ length: lanes }, (_, i) => (
        <div key={i} className="w-[248px] shrink-0 space-y-2 rounded-lg border bg-muted p-2">
          <Skeleton className="h-7 w-28" />
          {[0, 1, 2].map((c) => <Skeleton key={c} className="h-24 w-full rounded-lg bg-card" />)}
        </div>
      ))}
    </div>
  )
}
