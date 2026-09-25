import { useQuery } from '@tanstack/react-query'
import { ArrowDown, ArrowUp, Plus, X } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { Empty, ErrorBanner, Loading, Page } from '@/components/hub/common'
import { NewTaskDialog } from '@/components/hub/new-item'
import { useItemPanel } from '@/components/hub/panel-host'
import { PeoplePicker } from '@/components/hub/people'
import { Key, PriorityBadge } from '@/components/hub/pills'
import { ViewMenu } from '@/components/hub/views'
import { useScope, useScopeProjects, WorkspaceTabs } from '@/components/hub/workspace'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
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
  return { sp, filters, set }
}

/** Filters shared by the cross-project list and board (FR-VIS-04: switching keeps scope and filters). */
function FilterBar({ extra }: { extra?: React.ReactNode }) {
  const scope = useScope()
  const projects = useScopeProjects(scope)
  const { filters: f, set } = useFilters()
  const [term, setTerm] = useState(f.q ?? '')
  const statuses = (f.status ?? '').split(',').filter(Boolean)
  const sel = 'h-8 rounded-md border bg-card px-2 text-sm'
  const chip = (k: string, label: string) => (
    <span key={k} className="inline-flex items-center gap-1 rounded-full border bg-muted px-2 py-0.5 text-xs">
      {label}<button type="button" className="rounded-full hover:bg-accent" aria-label={t('task.removeFilter', { name: label })} onClick={() => set(k, null)}><X className="size-3" /></button>
    </span>
  )
  const projectName = (id: string) => projects.data?.find((p) => p.id === id)?.projectNumber ?? t('scope.oneProject')
  return (
    <div className="space-y-2">
      <div className="flex flex-wrap items-center gap-2">
        <form onSubmit={(e) => { e.preventDefault(); set('q', term.trim() || null) }}>
          <Input type="search" className="h-8 w-52" placeholder={t('task.searchPlaceholder')} aria-label={t('common.search')} value={term}
            onChange={(e) => { setTerm(e.target.value); if (!e.target.value) set('q', null) }} />
        </form>
        <select className={cn(sel, 'max-w-48')} value={f.projectId ?? ''} onChange={(e) => set('projectId', e.target.value)} aria-label={t('common.project')}>
          <option value="">{t('ws.anyProject')}</option>{projects.data?.map((p) => <option key={p.id} value={p.id}>{p.projectNumber} {p.name}</option>)}
        </select>
        <select className={sel} value={statuses.length > 1 ? '' : f.status ?? ''} onChange={(e) => set('status', e.target.value)} aria-label={t('common.status')}>
          <option value="">{t('task.anyStatus')}</option>{TASK_STATUSES.map((x) => <option key={x} value={x}>{tv(x)}</option>)}
        </select>
        <select className={sel} value={f.priority ?? ''} onChange={(e) => set('priority', e.target.value)} aria-label={t('common.priority')}>
          <option value="">{t('task.anyPriority')}</option>{PRIORITIES.map((x) => <option key={x} value={x}>{tv(x)}</option>)}
        </select>
        <div className="w-48"><PeoplePicker compact value={f.assigneeId ?? null} onChange={(v) => set('assigneeId', v)} placeholder={t('task.anyAssignee')} label={t('field.AssigneeId')} /></div>
        {extra}
      </div>
      <div className="flex flex-wrap items-center gap-1.5" role="group" aria-label={t('task.quickFilters')}>
        {(['open', ...QUICK] as const).map((k) => (
          <button key={k} type="button" aria-pressed={f[k] === 'true'} onClick={() => set(k, f[k] === 'true' ? null : 'true')}
            className={cn('rounded-full border px-2.5 py-0.5 text-xs', f[k] === 'true' ? 'border-primary bg-primary text-primary-foreground' : 'bg-card hover:bg-muted')}>{k === 'open' ? t('tfilter.open') : t(`tquick.${k}`)}</button>
        ))}
        {statuses.length > 1 && chip('status', `${t('common.status')}: ${statuses.map(tv).join(', ')}`)}
        {f.projectId && !projects.data?.some((p) => p.id === f.projectId) && chip('projectId', projectName(f.projectId))}
        {f.createdBy && chip('createdBy', t('ws.createdByFilter'))}
        {f.dueFrom && chip('dueFrom', `${t('tfilter.dueFrom')}: ${fmtDate(f.dueFrom)}`)}
        {f.dueTo && chip('dueTo', `${t('tfilter.dueTo')}: ${fmtDate(f.dueTo)}`)}
        {f.q && chip('q', `“${f.q}”`)}
      </div>
    </div>
  )
}

/** Tasks (§36.1): the selected projects' combined task list, with project labels and guarded status changes. */
export function WorkspaceTasksPage() {
  const scope = useScope()
  const me = useMe()
  const openPanel = useItemPanel()
  const { sp, filters, set } = useFilters()
  const page = Number(sp.get('page') ?? 1)
  const sort = sp.get('sort') ?? undefined
  const params = { projects: scope.api, ...filters, sort, page, pageSize: 100 }
  const q = useQuery({ queryKey: ['wtasks', params], enabled: scope.ready, queryFn: () => get<PageOf<TaskRow>>(`tasks${qs(params)}`) })
  const actions = useTaskActions(() => q.refetch())
  const [creating, setCreating] = useState(false)
  const [field, dir] = (sort ?? 'dueDate:asc').split(':')
  const th = (k: string, label: string) => (
    <th scope="col" className="px-3 py-2 text-left font-medium" aria-sort={field === k ? (dir === 'desc' ? 'descending' : 'ascending') : undefined}>
      <button type="button" className="inline-flex items-center gap-1 hover:underline" onClick={() => set('sort', field === k && dir !== 'desc' ? `${k}:desc` : `${k}:asc`)}>
        {t(label)}{field === k && (dir === 'desc' ? <ArrowDown className="size-3" /> : <ArrowUp className="size-3" />)}
      </button>
    </th>
  )
  const rows = q.data?.items ?? []
  const pages = q.data ? Math.max(1, Math.ceil(q.data.totalCount / q.data.pageSize)) : 1
  return (
    <Page title={t('nav.tasks')} subtitle={q.data ? plural(q.data.totalCount, 'ws.oneTask', 'ws.nTasks', { n: q.data.totalCount }) : scope.label}
      actions={<><ViewMenu listType="workspace-tasks" extra={() => scope.params} /><ViewSwitch view="tasks" paths={PATHS} />
        {me.capabilities.createTask && <Button onClick={() => setCreating(true)}><Plus className="size-4" />{t('task.new')}</Button>}</>}>
      <WorkspaceTabs />
      <FilterBar />
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      {q.isPending ? <Loading rows={8} /> : rows.length === 0 ? <div className="rounded-lg border bg-card"><Empty>{t('ws.noTasks')}</Empty></div> : (
        <div className="overflow-x-auto rounded-lg border bg-card">
          <table className="w-full text-sm">
            <thead className="border-b bg-muted/40 text-xs text-muted-foreground">
              <tr>{th('key', 'col.key')}{th('name', 'col.name')}<th scope="col" className="px-3 py-2 text-left font-medium">{t('col.project')}</th>
                {th('status', 'col.status')}<th scope="col" className="px-3 py-2 text-left font-medium">{t('col.assignee')}</th>{th('dueDate', 'col.due')}{th('priority', 'col.priority')}
                {th('progress', 'col.progress')}<th scope="col" className="px-3 py-2 text-left font-medium"><span className="sr-only">{t('col.indicators')}</span></th></tr>
            </thead>
            <tbody className="divide-y">
              {rows.map((r) => (
                <tr key={r.id} className="hover:bg-muted/30">
                  <td className="whitespace-nowrap px-3 py-1.5"><button type="button" className="hover:underline" onClick={() => openPanel('Task', r.id)}><Key>{r.key}</Key></button></td>
                  <td className="max-w-[20rem] px-3 py-1.5"><button type="button" className="text-left font-medium hover:underline" onClick={() => openPanel('Task', r.id)}>{r.name}</button></td>
                  <td className="whitespace-nowrap px-3 py-1.5" title={r.projectName}><span className="key font-mono text-xs">{r.projectNumber}</span></td>
                  <td className="px-3 py-1.5"><StatusMenu r={r} onMove={actions.move} /></td>
                  <td className="whitespace-nowrap px-3 py-1.5">{r.assigneeName ?? <span className="text-warn">{t('ind.unassigned')}</span>}</td>
                  <td className={cn('whitespace-nowrap px-3 py-1.5 tabular-nums', r.state?.isOverdue && 'font-medium text-bad')}>{fmtDate(r.dueDate)}</td>
                  <td className="px-3 py-1.5"><PriorityBadge priority={r.priority} /></td>
                  <td className="px-3 py-1.5 tabular-nums">{r.progressPct}%</td>
                  <td className="px-3 py-1.5"><TaskIndicators r={r} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {pages > 1 && (
        <div className="flex items-center justify-end gap-2 text-sm">
          <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => set('page', String(page - 1))}>{t('common.previous')}</Button>
          <span className="tabular-nums text-muted-foreground">{t('common.pageOf', { page, pages })}</span>
          <Button variant="outline" size="sm" disabled={page >= pages} onClick={() => set('page', String(page + 1))}>{t('common.next')}</Button>
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
  const { sp, filters, set } = useFilters()
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
  const sel = 'h-8 rounded-md border bg-card px-2 text-sm'
  return (
    <Page title={t('nav.boards')} subtitle={q.data ? t('board.count', { n: rows.length }) : scope.label}
      actions={<><ViewMenu listType="workspace-board" extra={() => scope.params} /><ViewSwitch view="board" paths={PATHS} />
        {me.capabilities.createTask && <Button onClick={() => setCreating(true)}><Plus className="size-4" />{t('task.new')}</Button>}</>}>
      <WorkspaceTabs />
      <FilterBar extra={<>
        <select className={sel} value={swim} onChange={(e) => set('swim', e.target.value === 'none' ? null : e.target.value)} aria-label={t('board.swimlanes')}>
          {SWIM.map((x) => <option key={x} value={x}>{t(`board.swim.${x}`)}</option>)}
        </select>
        <select className={sel} value={sort} onChange={(e) => set('sort', e.target.value === 'dueDate' ? null : e.target.value)} aria-label={t('board.sort')}>
          <option value="dueDate">{t('board.sortDue')}</option><option value="priority">{t('board.sortPriority')}</option>
          <option value="board" disabled={!scope.ws}>{scope.ws ? t('board.sortManual') : t('ws.manualNeedsWorkspaceShort')}</option>
        </select>
        <label className="flex items-center gap-1.5 text-sm"><input type="checkbox" checked={side} onChange={(e) => set('side', e.target.checked ? '1' : null)} />{t('board.showSide')}</label>
      </>} />
      {q.data && q.data.total > MAX_CARDS && <div role="status" className="rounded-md border border-warn/30 bg-warn-bg px-3 py-2 text-sm text-warn">{t('board.tooMany', { n: q.data.total, max: MAX_CARDS })}</div>}
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      {q.isPending ? <Loading rows={6} /> : rows.length === 0 ? <div className="rounded-lg border bg-card"><Empty>{t('ws.noTasks')}</Empty></div> : (
        <div className="space-y-4 overflow-x-auto pb-2">
          {groups.map((g) => (
            <section key={g.id} aria-label={g.label || t('nav.boards')}>
              {g.label && <h2 className="mb-1.5 text-sm font-semibold">{g.label} <span className="text-xs font-normal text-muted-foreground">{g.rows.length}</span></h2>}
              <Swimlane rows={g.rows} columns={columns} onDrop={onDrop} onReorder={onReorder} onAdd={me.capabilities.createTask ? () => setCreating(true) : undefined}
                render={(r) => <CardBody r={r} showProject open={() => openPanel('Task', r.id)} openDeliverable={(id) => openPanel('Deliverable', id)} />} />
            </section>
          ))}
        </div>
      )}
      {creating && <NewTaskDialog projectId={filters.projectId} onClose={(id) => { setCreating(false); if (id) q.refetch() }} />}
      {dialog}
      {actions.dialogs}
    </Page>
  )
}
