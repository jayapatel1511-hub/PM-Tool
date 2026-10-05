import { useQuery, useQueryClient } from '@tanstack/react-query'
import { CheckCheck, Plus } from 'lucide-react'
import { useMemo, useState, type ReactNode } from 'react'
import { Link, useSearchParams } from 'react-router'
import { AttentionRows, type AttentionList } from '@/components/hub/attention'
import { AccentDot, ActiveFilters, ChipToggle, Empty, ErrorBanner, Field, FilterBar, Loading, Page, Section, Segmented, selectCls } from '@/components/hub/common'
import { NewTaskDialog } from '@/components/hub/new-item'
import { NotificationItem, useMarkRead, useOpenNotification, type NotificationRow } from '@/components/hub/notifications'
import { useItemPanel } from '@/components/hub/panel-host'
import { Chip, HealthPill, Key, PriorityBadge, StatusPill } from '@/components/hub/pills'
import { ViewMenu } from '@/components/hub/views'
import { useScope } from '@/components/hub/workspace'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { get, qs } from '@/lib/api'
import { useMe } from '@/lib/auth'
import { addDays, fmtDate, relative, today } from '@/lib/format'
import { plural, t, tv } from '@/lib/i18n'
import type { Page as PageOf } from '@/lib/types'
import { cn, type Accent } from '@/lib/utils'
import { useLaneDrop } from './projects/Board'
import type { DeliverableRow } from './projects/Deliverables'
import { ActionOwner, type ActionRow } from './projects/Meetings'
import { FollowLevelSelect } from './projects/Follow'
import { BUCKETS, PRIORITIES, StatusMenu, TASK_STATUSES, TaskIndicators, dueBucket, useTaskActions, type TaskRow } from './projects/Tasks'
import { WorkspaceCoordination } from './WorkspaceCoordination'
import { MyWeekStrip } from '@/components/planner/MyWeekStrip'

interface Work {
  person: { id: string; displayName: string; jobTitle?: string; isActive: boolean }; readOnly: boolean; today: string
  attention: AttentionList; tasks: TaskRow[]; reviews: { tasks: TaskRow[]; deliverables: DeliverableRow[] }; deliverables: DeliverableRow[]
  people: Record<string, string>; successors: { id: string; key: string; name: string; status: string; dueDate?: string; assigneeName?: string }[]
  decisions: { id: string; projectId: string; projectNumber: string; key: string; subject: string; status: string; requiredByDate: string; impactLevel: string; role: string; isOverdue: boolean; isDueSoon: boolean }[]
  actions: ActionRow[]
  projects: { id: string; projectNumber: string; name: string; status: string; roles: string[]; leads: string[]; follow?: { level: string; source: string } | null
    computedHealth: string; reportedHealth: string; nextMilestone?: { id: string; key: string; name: string; date: string } | null }[]
  milestones: { id: string; projectId: string; projectNumber: string; key: string; name: string; milestoneType: string; date: string; status?: string }[]
  completed: TaskRow[]
}

const SORTS = ['due', 'priority', 'project', 'activity']
const RANK: Record<string, number> = { Critical: 0, High: 1, Medium: 2, Low: 3 }

const TABS = ['overview', 'today', 'upcoming', 'overdue', 'completed', 'inbox', 'coordination'] as const
const WHO = ['mine', 'assigned', 'created'] as const

/** One readable list row (14 px, density-aware height) inside a section card. */
const ROW = 'flex min-h-(--row-min) flex-wrap items-center gap-x-3 gap-y-1 px-5 py-(--cell-py) text-sm hover:bg-muted'
/** A project's number with its stable identity mark. */
const ProjectTag = ({ id, number, name }: { id: string; number: string; name?: string }) => (
  <span className="inline-flex shrink-0 items-center gap-1.5" title={name}><AccentDot id={id} /><Key>{number}</Key></span>
)

/** My Work (§13.10, §36.7 FR-VIS-08): the overview of everything assigned to, waiting on or held up by one person, plus
 *  Today, Upcoming, Overdue and Completed task views, the Inbox of unread notifications, all within the selected scope. */
export function MyWorkPage() {
  const [sp] = useSearchParams()
  const tab = sp.get('userId') ? 'overview' : sp.get('tab') ?? 'overview'
  if (tab === 'coordination') return <><div className="px-4 pt-4 md:px-6 md:pt-6 xl:px-8 xl:pt-8"><MyWorkTabs /></div><WorkspaceCoordination /></>
  if (tab === 'inbox') return <Inbox />
  if (tab !== 'overview') return <TaskView tab={tab} />
  return <Overview />
}

/** The My Work tabs; each keeps the scope and the membership view. */
function MyWorkTabs() {
  const [sp] = useSearchParams()
  const tab = sp.get('tab') ?? 'overview'
  const link = (x: string) => {
    const n = new URLSearchParams()
    for (const k of ['projects', 'ws', 'who']) if (sp.get(k)) n.set(k, sp.get(k)!)
    if (x !== 'overview') n.set('tab', x)
    return `?${n}`
  }
  return (
    <nav aria-label={t('mywork.views')} className="no-print flex flex-wrap border-b">
      {TABS.map((x) => (
        <Link key={x} to={link(x)} aria-current={tab === x ? 'page' : undefined}
          className={cn('-mb-px inline-flex min-h-11 items-center whitespace-nowrap border-b-2 px-3 text-sm outline-offset-[-2px]',
            tab === x ? 'border-primary font-semibold text-foreground' : 'border-transparent text-muted-foreground hover:bg-muted hover:text-foreground')}>{t(`mywork.tab.${x}`)}</Link>
      ))}
    </nav>
  )
}

/** Only the selected projects' rows when the scope names projects (FR-VIS-02). */
function scopeWork(w: Work, keep: (pid?: string | null) => boolean): Work {
  const attention = w.attention.items.filter((a) => keep(a.projectId))
  return {
    ...w, attention: { ...w.attention, items: attention, total: attention.length === w.attention.items.length ? w.attention.total : attention.length }, tasks: w.tasks.filter((r) => keep(r.projectId)),
    reviews: { tasks: w.reviews.tasks.filter((r) => keep(r.projectId)), deliverables: w.reviews.deliverables.filter((d) => keep(d.projectId)) },
    deliverables: w.deliverables.filter((d) => keep(d.projectId)), decisions: w.decisions.filter((d) => keep(d.projectId)), actions: w.actions.filter((a) => keep(a.projectId)),
    projects: w.projects.filter((p) => keep(p.id)),
    milestones: w.milestones.filter((m) => keep(m.projectId)), completed: w.completed.filter((r) => keep(r.projectId)),
  }
}

function Overview() {
  const me = useMe()
  const qc = useQueryClient()
  const [sp, setSp] = useSearchParams()
  const openPanel = useItemPanel()
  const scope = useScope()
  const keep = (pid?: string | null) => !scope.ids || (!!pid && scope.ids.includes(pid))
  const userId = sp.get('userId') ?? undefined
  const q = useQuery({ queryKey: ['mywork', userId], queryFn: () => get<Work>(`me/work${qs({ userId })}`) })
  const [creating, setCreating] = useState(false)
  const [filtersOpen, setFiltersOpen] = useState(false)
  const set = (k: string, v?: string) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); setSp(n, { replace: true }) }
  const f = { project: sp.get('project') ?? '', discipline: sp.get('discipline') ?? '', status: sp.get('status') ?? '', priority: sp.get('priority') ?? '',
    from: sp.get('from') ?? '', to: sp.get('to') ?? '', hideWaiting: sp.get('hideWaiting') === '1', sort: sp.get('sort') ?? 'due' }
  const reload = () => { qc.invalidateQueries({ queryKey: ['mywork'] }) }
  const actions = useTaskActions(reload)

  const tasks = useMemo(() => {
    const rows = (q.data?.tasks ?? []).filter((r) => (!scope.ids || scope.ids.includes(r.projectId)) && (!f.project || r.projectId === f.project) && (!f.discipline || r.disciplineName === f.discipline)
      && (!f.status || r.status === f.status) && (!f.priority || r.priority === f.priority) && (!f.from || (r.dueDate ?? '') >= f.from) && (!f.to || (r.dueDate ?? '9999') <= f.to)
      && (!f.hideWaiting || !(r.state?.isWaiting || r.state?.isBlocked)))
    const urgent = (r: TaskRow) => (r.state?.isOverdue ? 0 : r.dueDate === today() ? 1 : 2) // overdue and due today first
    const by: Record<string, (a: TaskRow, b: TaskRow) => number> = {
      due: (a, b) => (a.dueDate ?? '9999').localeCompare(b.dueDate ?? '9999'), priority: (a, b) => RANK[a.priority] - RANK[b.priority],
      project: (a, b) => a.projectNumber.localeCompare(b.projectNumber), activity: (a, b) => b.lastActivityAt.localeCompare(a.lastActivityAt),
    }
    return [...rows].sort((a, b) => urgent(a) - urgent(b) || (by[f.sort] ?? by.due)(a, b))
  }, [q.data, scope.ids, f.project, f.discipline, f.status, f.priority, f.from, f.to, f.hideWaiting, f.sort])

  if (q.isPending) return <Page title={t('mywork.title')}>{!userId && <MyWorkTabs />}<Loading rows={10} /></Page>
  if (q.error) return <Page title={t('mywork.title')}>{!userId && <MyWorkTabs />}<ErrorBanner error={q.error} retry={() => q.refetch()} /></Page>
  const w = scopeWork(q.data, keep)
  const ro = w.readOnly
  const waiting = tasks.filter((r) => r.state?.isWaiting || r.state?.isBlocked)
  const blocking = (w.tasks).filter((r) => r.state?.isBlocking)
  const buckets = BUCKETS.map((b) => [b, tasks.filter((r) => dueBucket(r, w.today) === b)] as const).filter(([, rows]) => rows.length > 0)
  const projectOptions = [...new Map(w.tasks.map((r) => [r.projectId, r.projectNumber])).entries()]
  const disciplineOptions = [...new Set(w.tasks.map((r) => r.disciplineName))]

  // Active filters as removable tokens (§13.0 Filters); sort is an order, not a filter.
  const tokens = ([
    ['project', t('common.project'), projectOptions.find(([id]) => id === f.project)?.[1]],
    ['discipline', t('common.discipline'), f.discipline],
    ['status', t('common.status'), f.status && tv(f.status)],
    ['priority', t('common.priority'), f.priority && tv(f.priority)],
    ['from', t('tfilter.dueFrom'), f.from && fmtDate(f.from)],
    ['to', t('tfilter.dueTo'), f.to && fmtDate(f.to)],
    ['hideWaiting', t('mywork.hideWaiting'), f.hideWaiting && t('common.yes')],
  ] as const).filter(([k]) => sp.get(k))
  const clear = () => { const n = new URLSearchParams(sp); for (const [k] of tokens) n.delete(k); setSp(n, { replace: true }) }
  const newTask = !ro && me.capabilities.createTask && <Button variant="outline" onClick={() => setCreating(true)}><Plus className="size-4" />{t('task.new')}</Button>

  const taskRow = (r: TaskRow, extra?: ReactNode) => (
    <li key={r.id} className={ROW}>
      <ProjectTag id={r.projectId} number={r.projectNumber} name={r.projectName} />
      <button type="button" className="hover:underline" onClick={() => openPanel('Task', r.id)}><Key>{r.key}</Key></button>
      <button type="button" className="min-w-[10rem] flex-1 break-words text-left font-medium hover:underline" onClick={() => openPanel('Task', r.id)}>{r.name}</button>
      {extra}
      <span className={cn('tabular-nums', r.state?.isOverdue && 'font-semibold text-bad', r.state?.isDueSoon && !r.state.isOverdue && 'text-warn')}>{fmtDate(r.dueDate)}</span>
      <StatusMenu r={r} onMove={actions.move} disabled={ro} />
      {!ro && r.status !== 'Complete' ? (
        <select className="h-(--control-row-h) rounded-md border border-input bg-card px-2 text-sm tabular-nums" value={r.progressPct}
          aria-label={t('task.progressOf', { key: r.key })} onChange={(e) => actions.setProgress(r, Number(e.target.value))}>
          {[0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100].map((x) => <option key={x} value={x}>{x}%</option>)}
        </select>
      ) : <span className="tabular-nums text-muted-foreground">{r.progressPct}%</span>}
      <PriorityBadge priority={r.priority === 'High' || r.priority === 'Critical' ? r.priority : null} />
      <TaskIndicators r={r} />
    </li>
  )
  const delRow = (d: DeliverableRow) => (
    <li key={d.id} className={ROW}>
      <button type="button" className="hover:underline" onClick={() => openPanel('Deliverable', d.id)}><Key>{d.key}</Key></button>
      <button type="button" className="min-w-[10rem] flex-1 break-words text-left font-medium hover:underline" onClick={() => openPanel('Deliverable', d.id)}>{d.name}</button>
      <span className={cn('tabular-nums', d.state?.isOverdue && 'font-semibold text-bad')}>{fmtDate(d.dueDate)}</span><StatusPill status={d.status} />
    </li>
  )
  const list = (rows: ReactNode[], empty: string) => rows.length ? <ul className="divide-y">{rows}</ul> : <Empty>{empty}</Empty>
  const sections: { id: string; title: string; count?: number; body: ReactNode; open?: boolean; accent?: Accent }[] = [
    ...(!userId ? [{ id: 'my-week', title: t('planner.myWeek'), body: <MyWeekStrip personId={me.id} canPlan={me.capabilities.workload} />, accent: 'blue' as Accent }] : []),
    { id: 'attention', title: t('mywork.attention'), count: w.attention.total, accent: 'peach', body: <>
      <AttentionRows items={w.attention.items} canSnooze={false} showProject onChanged={reload} />
      {w.attention.total > w.attention.items.length && <p className="border-t px-5 py-2.5 text-xs/[18px] text-muted-foreground">{t('mywork.attentionMore', { n: w.attention.items.length, total: w.attention.total })}</p>}
    </> },
    { id: 'tasks', title: t('mywork.tasks'), count: tasks.length, accent: 'blue', body: buckets.length === 0
      ? <Empty action={tokens.length > 0 ? <Button variant="outline" onClick={clear}>{t('filters.clear')}</Button> : newTask}>{t('mywork.noTasks')}</Empty>
      : <div className="divide-y">{buckets.map(([b, rows]) => (
        <div key={b}>
          <h3 className={cn('border-b bg-muted px-5 py-2 text-xs/[18px] font-semibold', b === 'overdue' ? 'text-bad' : 'text-muted-foreground')}>
            {b === 'overdue' && <span aria-hidden>■ </span>}{t(`tbucket.${b}`)} <span className="tabular-nums">({rows.length})</span>
          </h3>
          <ul className="divide-y">{rows.map((r) => taskRow(r))}</ul>
        </div>))}</div> },
    { id: 'reviews', title: t('mywork.reviews'), count: w.reviews.tasks.length + w.reviews.deliverables.length, accent: 'lavender', body: list([
      ...w.reviews.tasks.map((r) => taskRow(r, !ro && r.status === 'Ready for Review' && <Button size="sm" variant="outline"
        onClick={() => actions.move({ id: r.id, key: r.key, rowVersion: r.rowVersion, to: 'In Review', needsReason: false })}>{t('task.startReview')}</Button>)),
      ...w.reviews.deliverables.map(delRow)], t('mywork.noReviews')) },
    { id: 'deliverables', title: t('mywork.deliverables'), count: w.deliverables.length, body: list(w.deliverables.map(delRow), t('mywork.noDeliverables')) },
    { id: 'waiting', title: t('mywork.waiting'), count: waiting.length, body: list(waiting.map((r) => taskRow(r, (
      <span className="w-full text-xs/[18px] text-muted-foreground sm:w-auto">{(r.state?.blockedBy ?? []).map((b, i) => (
        <span key={i} className="mr-2">{b.type === 'manual' ? t('task.manualBlocker', { type: tv(b.name ?? ''), reason: b.reason ?? '' }) : `${b.key} ${b.name}`}{b.ownerId && ` · ${w.people[b.ownerId] ?? ''}`}</span>
      ))}</span>))), t('mywork.noWaiting')) },
    { id: 'blocking', title: t('mywork.blocking'), count: blocking.length, body: list(blocking.map((r) => taskRow(r, (
      <span className="w-full text-xs/[18px] text-bad sm:w-auto">{r.state!.blockingTaskIds.map((id) => w.successors.find((x) => x.id === id)).filter(Boolean).map((x) => `${x!.key} ${x!.name}${x!.assigneeName ? ` · ${x!.assigneeName}` : ''}`).join('; ')}</span>))), t('mywork.noBlocking')) },
    { id: 'decisions', title: t('mywork.decisions'), count: w.decisions.length, body: list(w.decisions.map((d) => (
      <li key={d.id} className={ROW}>
        <ProjectTag id={d.projectId} number={d.projectNumber} />
        <button type="button" className="min-w-[10rem] flex-1 break-words text-left hover:underline" onClick={() => openPanel('Decision', d.id)}><Key>{d.key}</Key> <span className="font-medium">{d.subject}</span></button>
        <span className="text-xs/[18px] text-muted-foreground">{t(`mywork.role.${d.role}`)}</span>
        <span className={cn('tabular-nums', d.isOverdue && 'font-semibold text-bad', d.isDueSoon && !d.isOverdue && 'text-warn')}>{t('wc.requiredBy', { date: fmtDate(d.requiredByDate) })}</span>
        {d.isOverdue ? <Chip tone="bad">{t('ind.overdue')}</Chip> : d.isDueSoon && <Chip tone="warn">{t('ind.dueSoon')}</Chip>}
        <StatusPill status={d.status} />
      </li>)), t('mywork.noDecisions')) },
    { id: 'actions', title: t('mywork.actions'), count: w.actions.length, body: list(w.actions.map((a) => ( // MTG-01
      <li key={a.id} className={ROW}>
        <ProjectTag id={a.projectId} number={a.projectNumber} />
        <button type="button" className="min-w-[10rem] flex-1 break-words text-left hover:underline" onClick={() => openPanel('Action', a.id)}><Key>{a.key}</Key> <span className="font-medium">{a.text}</span></button>
        {a.ownerType !== 'User' && <ActionOwner a={a} />}
        <span className="text-xs/[18px] text-muted-foreground">{a.meetingTitle}</span>
        <span className={cn('tabular-nums', a.isOverdue && 'font-semibold text-bad')}>{fmtDate(a.dueDate)}</span>
        {a.isOverdue && <Chip tone="bad">{t('ind.overdueD', { n: a.daysOverdue })}</Chip>}
        <StatusPill status={a.status} />
      </li>)), t('mywork.noActions')) },
    { id: 'projects', title: t('mywork.projects'), count: w.projects.length, accent: 'mint', body: list(w.projects.map((p) => (
      <li key={p.id} className={ROW}>
        <Link className="flex min-w-[10rem] flex-1 items-center gap-2 hover:underline" to={`/projects/${p.projectNumber}`}>
          <AccentDot id={p.id} /><Key>{p.projectNumber}</Key><span className="min-w-0 truncate font-medium">{p.name}</span>
        </Link>
        <span className="flex flex-wrap gap-1">{p.roles.map((r) => <span key={r} className="rounded-md bg-secondary px-2 py-0.5 text-xs/[18px] font-medium">{t(`role.${r}`)}</span>)}</span>
        <HealthPill health={p.reportedHealth} />
        {p.nextMilestone && <span className="text-xs/[18px] text-muted-foreground"><span aria-hidden>◆ </span>{p.nextMilestone.name} {relative(p.nextMilestone.date)}</span>}
        {!ro && <span className="w-44"><FollowLevelSelect projectId={p.id} level={p.follow?.level} onChanged={reload} projectName={`${p.projectNumber} ${p.name}`} /></span>}
      </li>)), t('mywork.noProjects')) },
    { id: 'milestones', title: t('mywork.milestones'), count: w.milestones.length, body: list(w.milestones.map((m) => (
      <li key={m.id} className={ROW}>
        <ProjectTag id={m.projectId} number={m.projectNumber} /><Key>{m.key}</Key><span className="min-w-[10rem] flex-1 break-words font-medium">{m.name}</span>
        <span className="tabular-nums">{fmtDate(m.date)} <span className="text-muted-foreground">({relative(m.date)})</span></span><StatusPill status={m.status} />
      </li>)), t('mywork.noMilestones')) },
    { id: 'completed', title: t('mywork.completed'), count: w.completed.length, open: false, body: list(w.completed.map((r) => taskRow(r)), t('mywork.noCompleted')) },
  ]

  return (
    <Page title={ro ? t('mywork.of', { name: w.person.displayName }) : t('mywork.title')} subtitle={ro ? t('mywork.readOnly') : me.displayName}
      actions={!ro && <><ViewMenu listType="mywork" extra={() => scope.params} />{me.capabilities.createTask && <Button onClick={() => setCreating(true)}><Plus className="size-4" />{t('task.new')}</Button>}</>}>
      {!ro && <MyWorkTabs />}
      <Button className="self-start sm:hidden" variant="outline" aria-expanded={filtersOpen} aria-controls="mw-filters" onClick={() => setFiltersOpen((open) => !open)}>{t('mywork.filters', { n: tokens.length })}</Button>
      <div id="mw-filters" className={cn(!filtersOpen && 'hidden sm:block')}>
      <FilterBar>
        <div className="flex flex-wrap items-end gap-3">
          <Field label={t('common.project')} htmlFor="mw-project" className="w-full sm:w-40">
            <select id="mw-project" className={selectCls} value={f.project} onChange={(e) => set('project', e.target.value || undefined)}>
              <option value="">{t('notif.anyProject')}</option>{projectOptions.map(([id, num]) => <option key={id} value={id}>{num}</option>)}
            </select>
          </Field>
          <Field label={t('common.discipline')} htmlFor="mw-discipline" className="w-full sm:w-44">
            <select id="mw-discipline" className={selectCls} value={f.discipline} onChange={(e) => set('discipline', e.target.value || undefined)}>
              <option value="">{t('projects.anyDiscipline')}</option>{disciplineOptions.map((d) => <option key={d} value={d}>{d}</option>)}
            </select>
          </Field>
          <Field label={t('common.status')} htmlFor="mw-status" className="w-full sm:w-44">
            <select id="mw-status" className={selectCls} value={f.status} onChange={(e) => set('status', e.target.value || undefined)}>
              <option value="">{t('task.anyOpenStatus')}</option>{TASK_STATUSES.filter((x) => x !== 'Complete' && x !== 'Cancelled').map((x) => <option key={x} value={x}>{tv(x)}</option>)}
            </select>
          </Field>
          <Field label={t('common.priority')} htmlFor="mw-priority" className="w-full sm:w-36">
            <select id="mw-priority" className={selectCls} value={f.priority} onChange={(e) => set('priority', e.target.value || undefined)}>
              <option value="">{t('task.anyPriority')}</option>{PRIORITIES.map((x) => <option key={x} value={x}>{tv(x)}</option>)}
            </select>
          </Field>
          <Field label={t('tfilter.dueFrom')} htmlFor="mw-from" className="w-full sm:w-40">
            <Input id="mw-from" type="date" value={f.from} onChange={(e) => set('from', e.target.value || undefined)} />
          </Field>
          <Field label={t('tfilter.dueTo')} htmlFor="mw-to" className="w-full sm:w-40">
            <Input id="mw-to" type="date" value={f.to} onChange={(e) => set('to', e.target.value || undefined)} />
          </Field>
          <Field label={t('mywork.sort')} htmlFor="mw-sort" className="w-full sm:w-44">
            <select id="mw-sort" className={selectCls} value={f.sort} onChange={(e) => set('sort', e.target.value === 'due' ? undefined : e.target.value)}>
              {SORTS.map((x) => <option key={x} value={x}>{t(`mywork.sort.${x}`)}</option>)}
            </select>
          </Field>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <ChipToggle on={f.hideWaiting} onClick={() => set('hideWaiting', f.hideWaiting ? undefined : '1')}>{t('mywork.hideWaiting')}</ChipToggle>
        </div>
      </FilterBar>
      </div>
      <ActiveFilters tokens={tokens.map(([key, label, value]) => ({ key, label, value: value || t('common.dash') }))} onRemove={(k) => set(k)} onClear={clear} />
      <nav aria-label={t('mywork.sections')} className="flex flex-wrap gap-2">
        {sections.map((s) => (
          <a key={s.id} href={`#mw-${s.id}`} className="inline-flex min-h-(--control-row-h) items-center gap-1.5 rounded-md border bg-card px-3 text-sm hover:bg-muted">
            {s.title}{(s.count ?? 0) > 0 && <span className="rounded-md bg-secondary px-1.5 text-xs/[18px] font-medium text-muted-foreground tabular-nums">{s.count}</span>}
          </a>
        ))}
      </nav>
      {sections.map((s) => s.open === false
        ? <details key={s.id} id={`mw-${s.id}`} className="overflow-hidden rounded-lg border bg-card">
            <summary className="cursor-pointer px-5 py-3 text-base/6 font-semibold hover:bg-muted">
              {s.title}<span className="ml-2 rounded-md bg-secondary px-2 py-0.5 text-xs font-medium text-muted-foreground tabular-nums">{s.count}</span>
            </summary>
            <div className="border-t">{s.body}</div>
          </details>
        : <Section key={s.id} id={`mw-${s.id}`} title={s.title} count={s.count} accent={s.accent}>{s.body}</Section>)}
      {creating && <NewTaskDialog onClose={() => { setCreating(false); reload() }} />}
      {actions.dialogs}
    </Page>
  )
}

/** Today, Upcoming, Overdue and Completed (FR-VIS-08) for one membership view: My Tasks (assignee or collaborator),
 *  Assigned to Me (assignee) or Created by Me (creator). The checkbox completes through the guarded transition. */
function TaskView({ tab }: { tab: string }) {
  const me = useMe()
  const qc = useQueryClient()
  const scope = useScope()
  const [sp, setSp] = useSearchParams()
  const openPanel = useItemPanel()
  const [creating, setCreating] = useState(false)
  const set = (k: string, v?: string | null) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); setSp(n, { replace: true }) }
  const who = (WHO as readonly string[]).includes(sp.get('who') ?? '') ? sp.get('who')! : 'mine'
  const now = today()
  const from = sp.get('from') ?? addDays(now, -14), to = sp.get('to') ?? now
  const whoParams = who === 'assigned' ? { assigneeId: me.id } : who === 'created' ? { createdBy: me.id } : { mine: true }
  const tabParams = tab === 'today' ? { dueFrom: now, dueTo: now, open: true } : tab === 'upcoming' ? { dueFrom: addDays(now, 1), open: true }
    : tab === 'overdue' ? { overdue: true } : { status: 'Complete', completedFrom: from, completedTo: to, sort: 'completed:desc' }
  const params = { projects: scope.api, ...whoParams, ...tabParams, pageSize: 200 }
  const q = useQuery({ queryKey: ['mywork-tasks', params], enabled: scope.ready, queryFn: () => get<PageOf<TaskRow>>(`tasks${qs(params)}`) })
  const actions = useTaskActions(() => { q.refetch(); qc.invalidateQueries({ queryKey: ['mywork'] }) })
  const lane = useLaneDrop(actions)
  const rows = q.data?.items ?? []
  return (
    <Page title={t('mywork.title')} subtitle={me.displayName}
      actions={<><ViewMenu listType="mywork" extra={() => scope.params} />{me.capabilities.createTask && <Button onClick={() => setCreating(true)}><Plus className="size-4" />{t('task.new')}</Button>}</>}>
      <MyWorkTabs />
      <FilterBar>
        <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
          <Segmented label={t('mywork.show')} value={who} onChange={(x) => set('who', x === 'mine' ? null : x)} options={WHO.map((x) => ({ value: x, label: t(`mywork.who.${x}`) }))} />
          <p className="text-sm text-muted-foreground">{t(`mywork.who.${who}.about`)}</p>
        </div>
        {tab === 'completed' && (
          <div className="flex flex-wrap items-end gap-3">
            <Field label={t('common.from')} htmlFor="mw-completed-from" className="w-full sm:w-44">
              <Input id="mw-completed-from" type="date" value={from} onChange={(e) => set('from', e.target.value || null)} />
            </Field>
            <Field label={t('common.to')} htmlFor="mw-completed-to" className="w-full sm:w-44">
              <Input id="mw-completed-to" type="date" value={to} onChange={(e) => set('to', e.target.value || null)} />
            </Field>
          </div>
        )}
      </FilterBar>
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      <Section title={t(`mywork.tab.${tab}`)} count={q.data?.totalCount} accent="blue">
        {q.isPending ? <Loading rows={5} /> : rows.length === 0
          ? <Empty action={me.capabilities.createTask && <Button variant="outline" onClick={() => setCreating(true)}><Plus className="size-4" />{t('task.new')}</Button>}>{t(`mywork.empty.${tab}`)}</Empty> : (
          <ul className="divide-y">
            {rows.map((r) => (
              <li key={r.id} className={ROW}>
                <Checkbox checked={r.status === 'Complete'} disabled={r.status === 'Complete' || r.status === 'Cancelled'} aria-label={t('mywork.complete', { key: r.key, name: r.name })}
                  onCheckedChange={() => lane.onDrop(r, 'Done', () => undefined)} />
                <button type="button" className="hover:underline" onClick={() => openPanel('Task', r.id)}><Key>{r.key}</Key></button>
                <button type="button" className="min-w-[10rem] flex-1 break-words text-left font-medium hover:underline" onClick={() => openPanel('Task', r.id)}>{r.name}</button>
                <ProjectTag id={r.projectId} number={r.projectNumber} name={r.projectName} />
                <span className={cn('tabular-nums', r.state?.isOverdue && 'font-semibold text-bad')}>{tab === 'completed' ? fmtDate(r.completedAt?.slice(0, 10)) : fmtDate(r.dueDate)}</span>
                <PriorityBadge priority={r.priority} />
                <StatusPill status={r.status} />
              </li>
            ))}
          </ul>
        )}
      </Section>
      {creating && <NewTaskDialog onClose={(id) => { setCreating(false); if (id) q.refetch() }} />}
      {lane.dialog}
      {actions.dialogs}
    </Page>
  )
}

/** Inbox (FR-VIS-08): unread personal notifications in the selected scope; opening one marks it read. */
function Inbox() {
  const me = useMe()
  const scope = useScope()
  const q = useQuery({ queryKey: ['notifications', 'inbox'], queryFn: () => get<PageOf<NotificationRow>>('me/notifications?unread=true&pageSize=100') })
  const open = useOpenNotification()
  const markRead = useMarkRead()
  const rows = (q.data?.items ?? []).filter((n) => !scope.ids || !n.projectId || scope.ids.includes(n.projectId))
  return (
    <Page title={t('mywork.title')} subtitle={me.displayName}
      actions={<><Button variant="outline" disabled={rows.length === 0} onClick={() => markRead({ ids: rows.map((n) => n.id) })}><CheckCheck className="size-4" />{t('mywork.markAllRead')}</Button>
        <Button asChild variant="ghost"><Link to="/notifications">{t('mywork.allNotifications')}</Link></Button></>}>
      <MyWorkTabs />
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      <Section title={t('mywork.tab.inbox')} count={rows.length}>
        {q.isPending ? <Loading rows={5} /> : rows.length === 0 ? <Empty>{t('mywork.empty.inbox')}</Empty>
          : <ul className="divide-y">{rows.map((n) => <li key={n.id}><NotificationItem n={n} onOpen={open} /></li>)}</ul>}
      </Section>
      {q.data && q.data.totalCount > q.data.items.length && <p className="text-xs/[18px] text-muted-foreground">{plural(q.data.totalCount, 'mywork.moreUnreadOne', 'mywork.moreUnread', { n: q.data.totalCount })}</p>}
    </Page>
  )
}
