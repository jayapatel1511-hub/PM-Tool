import { PersonPanelTrigger } from '@/components/hub/person-panel-trigger'
import { TaskBulkCommandDialog, TaskBulkToolbar, useTaskBulkSelection, type TaskBulkOperation } from '@/components/hub/task-bulk-command'
import { useQuery } from '@tanstack/react-query'
import { ArrowDown, ArrowUp, Columns3, Plus } from 'lucide-react'
import { Fragment, useMemo, useState } from 'react'
import { useLocation, useNavigate, useSearchParams } from 'react-router'
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
import { DropdownMenu, DropdownMenuCheckboxItem, DropdownMenuContent, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { typedRequest } from '@/lib/api'
import { useFlag, useMe } from '@/lib/auth'
import { addDays, fmtDate, hours, today } from '@/lib/format'
import { plural, t, tv , runtimeText } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import type { SavedViewKeys } from '@/gen/registry.gen'
import { AssigneeSwimlaneName, CardBody, LANES, MAX_CARDS, SIDE, Swimlane, fetchUpTo, useLaneDrop } from './projects/Board'
import { PINNED_QUICK, PRIORITIES, QUICK, MenuSelect, StatusMenu, TASK_STATUSES, TaskIndicators, ViewSwitch, errorText, useTaskActions, type TaskRow } from './projects/Tasks'
import { TaskActivityComments, TaskCommentCount, TaskLastActivity } from './projects/work-activity-comments'
import { TaskGroupSummary, TaskGroupReadStatus, groupHeading, taskGroupChoices, taskGroupEntries } from './projects/task-group-presentation'
import { BoardFiguresScope, BoardSwimlaneFacts, boardCardCount } from './projects/board-screen-presentation'
import { LEGACY_WORKSPACE_TASK_COLUMNS, REQUIRED_WORKSPACE_TASK_COLUMNS, normalizeWorkspaceTaskColumns, readWorkspaceTaskColumns, resolveWorkspaceTaskColumns, saveWorkspaceTaskColumns, type TaskSubmissionDateRow, type WorkspaceTaskColumnId } from './workspace-task-columns'

const PATHS = { tasks: '/tasks', board: '/boards' }
/** URL parameters that are task filters (everything else is scope, view or layout). */
const FILTERS = ['q', 'status', 'priority', 'assigneeId', 'disciplineId', 'projectId', 'createdBy', 'dueFrom', 'dueTo', 'onHold', 'open', 'withoutDueDate', 'homePreset', 'homeProjectStatus', ...QUICK] as const satisfies readonly (SavedViewKeys['workspace-tasks'] | 'withoutDueDate' | 'homePreset' | 'homeProjectStatus' | 'page' | 'panel' | 'draftAccessReason')[]

function useFilters() {
  const overview = useFlag('home_overview')
  const [sp, setSp] = useSearchParams()
  const supported = FILTERS.filter(k => !['withoutDueDate', 'disciplineId', 'homePreset', 'homeProjectStatus'].includes(k) || overview)
  const filters = Object.fromEntries(supported.flatMap((k) => (sp.get(k) ? [[k, sp.get(k)!]] : []))) as Record<string, string>
  const set = (k: string, v?: string | null) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); if (k !== 'page') n.delete('page'); setSp(n, { replace: true }) }
  const clear = () => { const n = new URLSearchParams(sp); for (const k of supported) n.delete(k); n.delete('page'); setSp(n, { replace: true }) }
  return { sp, filters, set, clear }
}

/** Filters shared by the cross-project list and board (FR-VIS-04: switching keeps scope and filters). `rows` name the
 *  people behind an assignee or creator filter. */
function TaskFilters({ rows, extra, lead, actions }: { rows: TaskRow[]; extra?: React.ReactNode; lead?: React.ReactNode; actions?: React.ReactNode }) {
  const overview = useFlag('home_overview')
  const scope = useScope()
  const projects = useScopeProjects(scope)
  const { filters: f, set, clear } = useFilters()
  // The box shows the applied search until someone types, so a removed token, Clear or a saved view updates it too.
  const [draft, setDraft] = useState<string | null>(null)
  const term = draft ?? f.q ?? ''
  const statuses = (f.status ?? '').split(',').filter(Boolean)
  const assigneeIds = [...new Set((f.assigneeId ?? '').split(',').filter(Boolean))]
  const disciplineIds = [...new Set((f.disciplineId ?? '').split(',').filter(Boolean))]
  const assignee = overview && assigneeIds.length > 1 ? t('bulk.selected', { n: assigneeIds.length }) : rows.find((r) => r.assigneeId === f.assigneeId)?.assigneeName
  const discipline = disciplineIds.length > 1 ? t('bulk.selected', { n: disciplineIds.length }) : rows.find(r => r.projectDisciplineId === f.disciplineId)?.disciplineName
  const preset = f.homePreset === 'ProjectManager' ? t('role.ProjectManager') : f.homePreset === 'DisciplineLead' ? t('role.DisciplineLead') : f.homePreset === 'Supervisor' ? t('role.Supervisor') : null
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
      ['disciplineId', t('common.discipline'), discipline],
      ['homePreset', runtimeText('home.rolePreset'), preset],
      ['homeProjectStatus', runtimeText('homeScope.projectStatus'), f.homeProjectStatus?.split(',').map(tv).join(', ')],
      ['createdBy', t('ws.createdBy'), rows.find((r) => r.createdBy === f.createdBy)?.createdByName],
      ['dueFrom', t('tfilter.dueFrom'), f.dueFrom && fmtDate(f.dueFrom)],
      ['dueTo', t('tfilter.dueTo'), f.dueTo && fmtDate(f.dueTo)],
      ['withoutDueDate', runtimeText('home.undatedTasks'), f.withoutDueDate === 'true' && t('common.yes')],
      ['onHold', t('tfilter.onHold'), f.onHold && t(f.onHold === 'true' ? 'common.yes' : 'common.no')],
    ] as const).filter(([k]) => f[k]).map(([key, label, value]) => ({ key, label, value: value || <Missing /> })),
    ...flags.map((k) => ({ key: k, label: k === 'open' ? t('tfilter.open') : t(`tquick.${k}`), value: t('common.yes') })),
  ]
  const chip = (k: 'open' | (typeof QUICK)[number]) => <ChipToggle key={k} on={f[k] === 'true'} onClick={() => set(k, f[k] === 'true' ? null : 'true')}>{k === 'open' ? t('tfilter.open') : t(`tquick.${k}`)}</ChipToggle>
  return (
    <FilterBar activeCount={tokens.length} lead={lead} actions={actions}
      search={<Field label={t('common.search')} htmlFor="wt-search">
        <form onSubmit={(e) => { e.preventDefault(); set('q', term.trim() || null); setDraft(null) }}>
          <Input id="wt-search" type="search" placeholder={t('task.searchPlaceholder')} value={term}
            onChange={(e) => { if (e.target.value) setDraft(e.target.value); else { setDraft(null); set('q', null) } }} />
        </form>
      </Field>}
      quick={<div className="flex flex-wrap items-center gap-2" role="group" aria-label={t('task.quickFilters')}>{PINNED_QUICK.map(chip)}</div>}
      active={<ActiveFilters tokens={tokens} onRemove={(k) => set(k, null)} onClear={clear} />}>
      <div className="flex flex-wrap items-end gap-3">
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
        <span id="wt-quick" className="mr-1 text-sm font-medium">{t('calm.moreQuick')}</span>
        {(['open', ...QUICK] as const).filter((k) => !(PINNED_QUICK as readonly string[]).includes(k)).map(chip)}
      </div>
    </FilterBar>
  )
}

/** Tasks (§36.1): the selected projects' combined task list, with project labels and guarded status changes. */
export function WorkspaceTasksPage() {
  const activityOn = useFlag('work_screens')
  const scope = useScope()
  const me = useMe()
  const location = useLocation()
  const navigate = useNavigate()
  const openPanel = useItemPanel()
  const { sp, filters, set, clear } = useFilters()
  const bulkScopeKey = JSON.stringify([me.id, scope.api, filters])
  const selection = useTaskBulkSelection(bulkScopeKey, activityOn)
  const [bulk, setBulk] = useState<TaskBulkOperation | null>(null)
  const page = Number(sp.get('page') ?? 1)
  const group = activityOn ? sp.get('group') ?? 'none' : 'none'
  const sort = sp.get('sort') ?? undefined
  const params = { projects: scope.api, ...filters, sort, page, pageSize: 100, ...(activityOn && group !== 'none' ? { group } : {}) }
  const q = useQuery({ queryKey: ['wtasks', params], enabled: scope.ready, queryFn: () => typedRequest('get', '/api/v1/tasks', { query: params }) })
  const actions = useTaskActions(() => q.refetch())
  const [creating, setCreating] = useState(false)
  const storedColumns = useMemo(() => activityOn ? readWorkspaceTaskColumns(me.id) : [...LEGACY_WORKSPACE_TASK_COLUMNS], [activityOn, me.id])
  const [chosenDefaults, setChosenDefaults] = useState<{ userId: string; columns: WorkspaceTaskColumnId[] } | null>(null)
  const defaults = chosenDefaults?.userId === me.id ? chosenDefaults.columns : storedColumns
  const columnIds = activityOn ? resolveWorkspaceTaskColumns(sp.get('cols'), defaults) : [...LEGACY_WORKSPACE_TASK_COLUMNS]
  const setColumns = (ids: WorkspaceTaskColumnId[]) => {
    const normalized = normalizeWorkspaceTaskColumns(ids)
    setChosenDefaults({ userId: me.id, columns: normalized })
    saveWorkspaceTaskColumns(me.id, normalized)
    const next = new URLSearchParams(sp); next.set('cols', normalized.join(','))
    navigate(`${location.pathname}?${next}${location.hash}`, { replace: true, preventScrollReset: true, state: location.state })
  }
  const [field, dir] = (sort ?? 'dueDate:asc').split(':')
  const th = (k: string, label: string, numeric?: boolean) => (
    <th scope="col" className={cn(thCls, numeric && 'text-right')} aria-sort={field === k ? (dir === 'desc' ? 'descending' : 'ascending') : undefined}>
      <button type="button" className="inline-flex items-center gap-1 hover:text-foreground" onClick={() => set('sort', field === k && dir !== 'desc' ? `${k}:desc` : `${k}:asc`)}>
        {runtimeText(label)}{field === k && (dir === 'desc' ? <ArrowDown className="size-3.5" /> : <ArrowUp className="size-3.5" />)}
      </button>
    </th>
  )
  const rows = q.data?.items ?? []
  const grouped = taskGroupEntries(rows, q.data?.groups)
  const entries = activityOn && group !== 'none' ? grouped.entries : rows.map(row => ({ kind: 'row' as const, row }))
  const pages = q.data ? Math.max(1, Math.ceil(q.data.totalCount / q.data.pageSize)) : 1
  const filtered = Object.keys(filters).length > 0
  const unavailable = <span className="text-muted-foreground">{t('common.unavailable')}</span>
  const dash = <span className="text-muted-foreground">{t('common.dash')}</span>
  const submissionDate = (r: TaskRow & TaskSubmissionDateRow) => {
    const fact = r.submissionDate
    if (fact?.state !== 'Known') return unavailable
    if (fact.targetDate === null) return <span className="text-muted-foreground">{runtimeText('ws.noOpenSubmission')}</span>
    return typeof fact.targetDate === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(fact.targetDate) ? fmtDate(fact.targetDate) : unavailable
  }
  const allColumns: { id: WorkspaceTaskColumnId; label: string; sort?: string; num?: boolean; cls?: string; cell: (r: TaskRow, surface?: 'table' | 'card') => React.ReactNode }[] = [
    { id: 'key', label: 'col.key', sort: 'key', cls: 'whitespace-nowrap', cell: r => <button type="button" data-task-bulk-return={r.id} className="hover:underline" onClick={() => openPanel('Task', r.id)}><Key>{r.key}</Key></button> },
    { id: 'name', label: 'col.name', sort: 'name', cls: 'min-w-48 max-w-[22rem]', cell: r => <button type="button" className="text-left font-medium hover:underline" onClick={() => openPanel('Task', r.id)}>{r.name}</button> },
    { id: 'project', label: 'col.project', cls: 'whitespace-nowrap', cell: r => <span className="inline-flex items-center gap-2" title={r.projectName}><AccentDot id={r.projectId} /><Key>{r.projectNumber}</Key></span> },
    { id: 'deliverable', label: 'task.deliverable', cell: r => !r.deliverableId ? dash : r.deliverableKey && r.deliverableName ? <button type="button" className="max-w-56 break-words text-left hover:underline" onClick={() => openPanel('Deliverable', r.deliverableId!)}><Key>{r.deliverableKey}</Key> {r.deliverableName}</button> : unavailable },
    { id: 'status', label: 'col.status', sort: 'status', cell: r => <StatusMenu r={r} onMove={actions.move} /> },
    { id: 'assignee', label: 'col.assignee', cls: 'whitespace-nowrap', cell: (r, surface = 'table') => r.assigneeName ? <span className="inline-flex items-center gap-2"><Avatar id={r.assigneeId} name={r.assigneeName} />{r.assigneeId ? <PersonPanelTrigger userId={r.assigneeId} name={r.assigneeName} focusKey={`workspace-task:${r.id}:assignee:${surface}`} /> : r.assigneeName}</span> : <span className="text-warn">{t('ind.unassigned')}</span> },
    { id: 'reviewer', label: 'field.ReviewerId', cell: (r, surface = 'table') => r.reviewerName ? <span className="inline-flex items-center gap-2"><Avatar id={r.reviewerId} name={r.reviewerName} />{r.reviewerId ? <PersonPanelTrigger userId={r.reviewerId} name={r.reviewerName} focusKey={`workspace-task:${r.id}:reviewer:${surface}`} /> : r.reviewerName}</span> : r.reviewerId ? unavailable : dash },
    { id: 'dueDate', label: 'col.due', sort: 'dueDate', cls: 'whitespace-nowrap tabular-nums', cell: r => <span className={cn(r.state?.isOverdue && 'font-medium text-bad')}>{fmtDate(r.dueDate)}</span> },
    { id: 'priority', label: 'col.priority', sort: 'priority', cell: r => <PriorityBadge priority={r.priority} /> },
    { id: 'estimate', label: 'task.estimate', sort: 'estimate', num: true, cls: 'tabular-nums', cell: r => r.estimatedHours == null ? <span className="text-muted-foreground">{t('task.unestimated')}</span> : hours(r.estimatedHours) },
    { id: 'progress', label: 'col.progress', sort: 'progress', num: true, cls: 'tabular-nums', cell: r => `${r.progressPct}%` },
    { id: 'indicators', label: 'col.indicators', cell: r => <TaskIndicators r={r} /> },
    { id: 'lastActivity', label: 'task.lastActivity', sort: 'lastActivity', cls: 'min-w-40', cell: (r, surface = 'table') => <TaskLastActivity row={r} focusKey={`workspace-task:${r.id}:activity:${surface}`} /> },
    { id: 'submissionDate', label: 'ws.submissionDate', cls: 'tabular-nums', cell: submissionDate },
    { id: 'comments', label: 'common.comments', cell: r => <TaskCommentCount row={r} /> },
  ]
  const columns = allColumns.filter(column => columnIds.includes(column.id))
  const groupName = (g: Parameters<typeof groupHeading>[0], surface: string) => g.dimension === 'assignee' && g.referenceAvailable && g.value && g.label
    ? <PersonPanelTrigger userId={g.value} name={g.label} focusKey={`workspace-task-group:${g.key}:${surface}:assignee`} /> : groupHeading(g)
  const columnChooser = activityOn && <DropdownMenu>
    <DropdownMenuTrigger asChild><Button variant="outline"><Columns3 className="size-4" aria-hidden />{t('task.columns')}</Button></DropdownMenuTrigger>
    <DropdownMenuContent align="end">
      {allColumns.filter(column => !REQUIRED_WORKSPACE_TASK_COLUMNS.includes(column.id)).map(column => <DropdownMenuCheckboxItem key={column.id} checked={columnIds.includes(column.id)}
        onSelect={event => event.preventDefault()} onCheckedChange={on => setColumns(on ? [...columnIds, column.id] : columnIds.filter(id => id !== column.id))}>{runtimeText(column.label)}</DropdownMenuCheckboxItem>)}
    </DropdownMenuContent>
  </DropdownMenu>
  return (
    <Page tabs={<WorkspaceTabs />} title={t('nav.tasks')} subtitle={q.data ? plural(q.data.totalCount, 'ws.oneTask', 'ws.nTasks', { n: q.data.totalCount }) : scope.label}
      actions={<><ViewMenu listType="workspace-tasks" extra={() => ({ ...scope.params, ...(activityOn ? { cols: columnIds.join(',') } : {}) })} /><ViewSwitch view="tasks" paths={PATHS} />
        {me.capabilities.createTask && <Button onClick={() => setCreating(true)}><Plus className="size-4" />{t('task.new')}</Button>}</>}>
      <TaskFilters rows={rows} actions={columnChooser} lead={activityOn && <MenuSelect label={t('common.groupBy')} value={group} options={taskGroupChoices()} onChange={value => set('group', value === 'none' ? null : value)} />} />
      {activityOn && <TaskBulkToolbar selected={selection.selected} pageIds={rows.map(row => row.id)} onAction={setBulk} onClear={selection.clear} />}
      {activityOn && group !== 'none' && q.data && <TaskGroupReadStatus asOf={q.data.asOf} complete={grouped.complete} />}
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      <div className={activityOn ? 'min-w-0 space-y-4 outline-offset-4' : 'contents'} data-task-bulk-list={activityOn ? '' : undefined} role={activityOn ? 'region' : undefined} aria-label={activityOn ? t('nav.tasks') : undefined} tabIndex={activityOn ? -1 : undefined}>
      {q.isPending ? <div className="rounded-lg border bg-card"><Loading rows={8} /></div> : rows.length === 0 ? (
        <div className="rounded-lg border bg-card"><Empty action={filtered && <Button variant="outline" onClick={clear}>{t('filters.clear')}</Button>}>{t('ws.noTasks')}</Empty></div>
      ) : <>
        {/* Phones get cards (§13.0 Responsive) with the same task, project, status and indicators. */}
        <ul className="space-y-2 md:hidden" aria-label={t('nav.tasks')}>
          {activityOn && <li><label className="inline-flex min-h-9 items-center gap-2 text-sm"><Checkbox aria-label={t('bulkCommand.selectPage')} checked={rows.length > 0 && rows.every(row => selection.selected.has(row.id))} onCheckedChange={on => selection.page(rows, on === true)} />{t('bulkCommand.selectPage')}</label></li>}
          {entries.map(entry => { if (entry.kind === 'group') return <li key={entry.group.key} data-task-group={entry.group.key} className="space-y-1 py-2"><h3 className="break-words text-sm font-semibold">{groupName(entry.group, 'card')}</h3><TaskGroupSummary summary={entry.group} shown={entry.shown} unavailable={entry.unavailable} /></li>
            const r = entry.row
            return (
            <li key={r.id} className="space-y-2 rounded-lg border bg-card p-4 text-sm">
              {activityOn && <Checkbox aria-label={`${t('bulk.select')} ${r.key}`} checked={selection.selected.has(r.id)} onCheckedChange={() => selection.toggle(r)} />}
              <p className="flex flex-wrap items-center gap-2" title={r.projectName}><AccentDot id={r.projectId} /><Key>{r.projectNumber}</Key><Key>{r.key}</Key></p>
              <button type="button" data-task-bulk-return={r.id} className="block text-left font-semibold hover:underline" onClick={() => openPanel('Task', r.id)}>{r.name}</button>
              {!activityOn && <div className="flex flex-wrap items-center gap-2"><StatusMenu r={r} onMove={actions.move} /><PriorityBadge priority={r.priority} /><TaskIndicators r={r} /></div>}
              {activityOn && <dl className="grid grid-cols-[auto_minmax(0,1fr)] gap-x-3 gap-y-2 text-xs/[18px]">{columns.filter(column => !REQUIRED_WORKSPACE_TASK_COLUMNS.includes(column.id)).map(column => <div key={column.id} className="contents" data-workspace-column={column.id}><dt className="text-muted-foreground">{runtimeText(column.label)}</dt><dd className="min-w-0 break-words">{column.cell(r, 'card')}</dd></div>)}</dl>}
              {!activityOn && <>
              <p className="flex flex-wrap gap-x-4 gap-y-1 text-xs/[18px] text-muted-foreground">
                {r.assigneeId && r.assigneeName ? <PersonPanelTrigger userId={r.assigneeId} name={r.assigneeName} focusKey={`workspace-task:${r.id}:assignee:card-legacy`} /> : r.assigneeName ?? <span className="text-warn">{t('ind.unassigned')}</span>}
                <span className={cn('tabular-nums', r.state?.isOverdue && 'font-medium text-bad')}>{t('col.due')} {fmtDate(r.dueDate)}</span>
                <span className="tabular-nums">{t('col.progress')} {r.progressPct}%</span>
              </p>
              <TaskActivityComments row={r} focusKey={`workspace-task:${r.id}:activity:card-legacy`} />
              </>}
            </li>
          )})}
        </ul>
        <TableRegion className="hidden md:block">
          <table className="w-full text-sm">
            <caption className="sr-only">{t('nav.tasks')}</caption>
            <thead className="bg-muted">
              <tr>{activityOn && <th scope="col" className={thCls}><Checkbox aria-label={t('bulkCommand.selectPage')} checked={rows.length > 0 && rows.every(row => selection.selected.has(row.id))} onCheckedChange={on => selection.page(rows, on === true)} /></th>}{columns.map(column => <Fragment key={column.id}>{column.sort ? th(column.sort, column.label, column.num) : <th scope="col" className={thCls}>{runtimeText(column.label)}</th>}</Fragment>)}</tr>
            </thead>
            <tbody>
              {entries.map(entry => { if (entry.kind === 'group') return <tr key={entry.group.key} data-task-group={entry.group.key} className="border-t bg-muted"><th scope="rowgroup" colSpan={columns.length + (activityOn ? 1 : 0)} className="space-y-1 px-4 py-2 text-left"><span className="block break-words text-sm font-semibold">{groupName(entry.group, 'table')}</span><TaskGroupSummary summary={entry.group} shown={entry.shown} unavailable={entry.unavailable} /></th></tr>
                const r = entry.row
                return (
                <tr key={r.id} className="border-t hover:bg-muted">
                  {activityOn && <td className={tdCls}><Checkbox aria-label={`${t('bulk.select')} ${r.key}`} checked={selection.selected.has(r.id)} onCheckedChange={() => selection.toggle(r)} /></td>}
                  {columns.map(column => <td key={column.id} data-workspace-column={column.id} className={cn(tdCls, column.cls, column.num && 'text-right')}>{column.cell(r)}</td>)}
                </tr>
              )})}
            </tbody>
          </table>
        </TableRegion>
      </>}
      </div>
      {pages > 1 && (
        <div className="flex items-center justify-end gap-3">
          <Button variant="outline" disabled={page <= 1} onClick={() => set('page', String(page - 1))}>{t('common.previous')}</Button>
          <span className="text-sm tabular-nums" aria-live="polite">{t('common.pageOf', { page, pages })}</span>
          <Button variant="outline" disabled={page >= pages} onClick={() => set('page', String(page + 1))}>{t('common.next')}</Button>
        </div>
      )}
      {creating && <NewTaskDialog projectId={filters.projectId} onClose={(id) => { setCreating(false); if (id) q.refetch() }} />}
      {activityOn && bulk && <TaskBulkCommandDialog operation={bulk} scopeKey={bulkScopeKey} rows={[...selection.selected.values()]} onChanged={() => q.refetch()} onClose={completed => { setBulk(null); if (completed) selection.clear() }} />}
      {actions.dialogs}
    </Page>
  )
}

const SWIM = ['none', 'project', 'assignee']

/** Boards (§36.3, FR-VIS-04): the selected projects' four-lane board with project labels on every card; moves are guarded
 *  transitions, and a named workspace keeps its owner's own card order. */
export function WorkspaceBoardPage() {
  const boardOn = useFlag('work_screens')
  const scope = useScope()
  const me = useMe()
  const openPanel = useItemPanel()
  const { sp, filters, set, clear } = useFilters()
  const side = sp.get('side') === '1'
  const swim = sp.get('swim') ?? 'none'
  const manual = sp.get('sort') === 'board' && !!scope.ws
  const sort = manual ? 'board' : sp.get('sort') === 'priority' ? 'priority' : 'dueDate'
  const params = { projects: scope.api, ...filters, sort, includeCancelled: side, doneSince: addDays(today(), -14), workspaceId: manual ? scope.ws!.id : undefined }
  const q = useQuery({ queryKey: ['wboard', params], enabled: scope.ready, queryFn: () => fetchUpTo((page, pageSize) => typedRequest('get', '/api/v1/tasks', { query: { ...params, page, pageSize } }), MAX_CARDS) })
  const actions = useTaskActions(() => q.refetch())
  const { onDrop, dialog } = useLaneDrop(actions)
  const [creating, setCreating] = useState(false)
  const lanes = side ? [...LANES, ...SIDE] : LANES
  const columns = lanes.map((l) => ({ id: l, name: runtimeText(`lane.${l}`) }))
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
    try { await typedRequest('put', '/api/v1/workspaces/{id}/board-order', { params: { id: scope.ws!.id }, body: { taskIds: ids } }); q.refetch() } catch (e) { snapBack(); toast.error(errorText(e)) }
  }
  return (
    <Page tabs={<WorkspaceTabs />} title={t('nav.boards')} subtitle={q.data ? t('board.count', { n: rows.length }) : scope.label}
      actions={<><ViewMenu listType="workspace-board" extra={() => scope.params} /><ViewSwitch view="board" paths={PATHS} />
        {me.capabilities.createTask && <Button onClick={() => setCreating(true)}><Plus className="size-4" />{t('task.new')}</Button>}</>}>
      <TaskFilters rows={all} extra={<>
        <Field label={t('board.swimlanes')} htmlFor="wb-swim" className="w-full sm:w-52">
          <select id="wb-swim" className={selectCls} value={swim} onChange={(e) => set('swim', e.target.value === 'none' ? null : e.target.value)}>
            {SWIM.map((x) => <option key={x} value={x}>{runtimeText(`board.swim.${x}`)}</option>)}
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
      {boardOn && <BoardFiguresScope />}
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
                  boardOn ? <div className="sticky left-0 w-fit max-w-[calc(100vw-4rem)] space-y-1"><h2 className="flex items-center gap-2 text-base/6 font-semibold">
                    {swim === 'project' ? <AccentDot id={g.id} /> : g.id && <Avatar id={g.id} name={g.label} />}
                    <span className="truncate">{swim === 'assignee' ? <AssigneeSwimlaneName rows={g.rows} id={g.id} label={g.label} focusKey={`workspace-board:${g.id}:assignee-group`} /> : g.label}</span>
                    <span aria-label={boardCardCount(g.rows.length)} className="rounded-md bg-secondary px-2 py-0.5 text-xs font-medium text-muted-foreground tabular-nums">{g.rows.length}</span>
                  </h2><BoardSwimlaneFacts rows={g.rows} kind={swim} focusKey={`workspace-board:${g.id}:lead`} /></div> : <h2 className="sticky left-0 flex w-fit max-w-[calc(100vw-4rem)] items-center gap-2 text-base/6 font-semibold">
                    {swim === 'project' ? <AccentDot id={g.id} /> : g.id && <Avatar id={g.id} name={g.label} />}
                    <span className="truncate">{swim === 'assignee' ? <AssigneeSwimlaneName rows={g.rows} id={g.id} label={g.label} focusKey={`workspace-board:${g.id}:assignee-group`} /> : g.label}</span><span className="rounded-md bg-secondary px-2 py-0.5 text-xs font-medium text-muted-foreground tabular-nums">{g.rows.length}</span>
                  </h2>
                )}
                {/* New task lives in the page header only; a per-lane copy would repeat it in every swimlane (§41 R1). */}
                <Swimlane rows={g.rows} columns={columns} onDrop={onDrop} onReorder={onReorder}
                  render={(r, overlay) => <CardBody r={r} personLinks={!overlay} showProject open={() => openPanel('Task', r.id)} openDeliverable={(id) => openPanel('Deliverable', id)} />} />
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
