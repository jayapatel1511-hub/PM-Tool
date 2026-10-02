import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { useMemo, useState, type ReactNode } from 'react'
import { Link, useSearchParams } from 'react-router'
import { AttentionRows, type AttentionList } from '@/components/hub/attention'
import { Empty, ErrorBanner, Loading, Page, Section } from '@/components/hub/common'
import { NewTaskDialog } from '@/components/hub/new-item'
import { NotificationItem, useMarkRead, useOpenNotification, type NotificationRow } from '@/components/hub/notifications'
import { useItemPanel } from '@/components/hub/panel-host'
import { HealthPill, Key, PriorityBadge, StatusPill } from '@/components/hub/pills'
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
import { cn } from '@/lib/utils'
import { useLaneDrop } from './projects/Board'
import type { DeliverableRow } from './projects/Deliverables'
import { ActionOwner, type ActionRow } from './projects/Meetings'
import { FollowLevelSelect } from './projects/Follow'
import { BUCKETS, PRIORITIES, StatusMenu, TASK_STATUSES, TaskIndicators, dueBucket, useTaskActions, type TaskRow } from './projects/Tasks'
import { WorkspaceCoordination } from './WorkspaceCoordination'

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

/** My Work (§13.10, §36.7 FR-VIS-08): the overview of everything assigned to, waiting on or held up by one person, plus
 *  Today, Upcoming, Overdue and Completed task views, the Inbox of unread notifications, all within the selected scope. */
export function MyWorkPage() {
  const [sp] = useSearchParams()
  const tab = sp.get('userId') ? 'overview' : sp.get('tab') ?? 'overview'
  if (tab === 'coordination') return <><MyWorkTabs /><WorkspaceCoordination /></>
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
    <nav aria-label={t('mywork.views')} className="flex flex-wrap border-b">
      {TABS.map((x) => (
        <Link key={x} to={link(x)} aria-current={tab === x ? 'page' : undefined}
          className={cn('-mb-px border-b-2 px-3 py-1.5 text-sm', tab === x ? 'border-primary font-medium' : 'border-transparent text-muted-foreground hover:text-foreground')}>{t(`mywork.tab.${x}`)}</Link>
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

  if (q.isPending) return <Loading rows={10} />
  if (q.error) return <div className="p-6"><ErrorBanner error={q.error} /></div>
  const w = scopeWork(q.data, keep)
  const ro = w.readOnly
  const waiting = tasks.filter((r) => r.state?.isWaiting || r.state?.isBlocked)
  const blocking = (w.tasks).filter((r) => r.state?.isBlocking)
  const buckets = BUCKETS.map((b) => [b, tasks.filter((r) => dueBucket(r, w.today) === b)] as const).filter(([, rows]) => rows.length > 0)
  const projectOptions = [...new Map(w.tasks.map((r) => [r.projectId, r.projectNumber])).entries()]
  const disciplineOptions = [...new Set(w.tasks.map((r) => r.disciplineName))]

  const taskRow = (r: TaskRow, extra?: ReactNode) => (
    <li key={r.id} className="flex flex-wrap items-center gap-x-3 gap-y-1 px-4 py-2 text-sm">
      <Key>{r.projectNumber}</Key>
      <button className="hover:underline" onClick={() => openPanel('Task', r.id)}><Key>{r.key}</Key></button>
      <button className="min-w-[10rem] flex-1 text-left font-medium hover:underline" onClick={() => openPanel('Task', r.id)}>{r.name}</button>
      {extra}
      <span className={cn('tabular-nums', r.state?.isOverdue && 'font-medium text-bad', r.state?.isDueSoon && !r.state.isOverdue && 'text-warn')}>{fmtDate(r.dueDate)}</span>
      <StatusMenu r={r} onMove={actions.move} disabled={ro} />
      {!ro && r.status !== 'Complete' ? (
        <select className="h-7 rounded border border-transparent bg-transparent px-1 text-[13px] tabular-nums hover:border-border" value={r.progressPct}
          aria-label={t('task.progressOf', { key: r.key })} onChange={(e) => actions.setProgress(r, Number(e.target.value))}>
          {[0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100].map((x) => <option key={x} value={x}>{x}%</option>)}
        </select>
      ) : <span className="tabular-nums text-muted-foreground">{r.progressPct}%</span>}
      <PriorityBadge priority={r.priority === 'High' || r.priority === 'Critical' ? r.priority : null} />
      <TaskIndicators r={r} />
    </li>
  )
  const delRow = (d: DeliverableRow) => (
    <li key={d.id} className="flex flex-wrap items-center gap-x-3 px-4 py-2 text-sm">
      <button className="hover:underline" onClick={() => openPanel('Deliverable', d.id)}><Key>{d.key}</Key></button>
      <button className="min-w-0 flex-1 text-left font-medium hover:underline" onClick={() => openPanel('Deliverable', d.id)}>{d.name}</button>
      <span className={cn('tabular-nums', d.state?.isOverdue && 'font-medium text-bad')}>{fmtDate(d.dueDate)}</span><StatusPill status={d.status} />
    </li>
  )
  const list = (rows: ReactNode[], empty: string) => rows.length ? <ul className="divide-y">{rows}</ul> : <Empty>{empty}</Empty>
  const sections: { id: string; title: string; count: number; body: ReactNode; open?: boolean }[] = [
    { id: 'attention', title: t('mywork.attention'), count: w.attention.total, body: <>
      <AttentionRows items={w.attention.items} canSnooze={false} showProject onChanged={reload} />
      {w.attention.total > w.attention.items.length && <p className="border-t px-4 py-2 text-xs text-muted-foreground">{t('mywork.attentionMore', { n: w.attention.items.length, total: w.attention.total })}</p>}
    </> },
    { id: 'tasks', title: t('mywork.tasks'), count: tasks.length, body: buckets.length === 0 ? <Empty>{t('mywork.noTasks')}</Empty> : buckets.map(([b, rows]) => (
      <div key={b}><div className={cn('bg-muted/40 px-4 py-1 text-xs font-semibold', b === 'overdue' ? 'text-bad' : 'text-muted-foreground')}>{t(`tbucket.${b}`)} ({rows.length})</div>
        <ul className="divide-y">{rows.map((r) => taskRow(r))}</ul></div>)) },
    { id: 'reviews', title: t('mywork.reviews'), count: w.reviews.tasks.length + w.reviews.deliverables.length, body: list([
      ...w.reviews.tasks.map((r) => taskRow(r, !ro && r.status === 'Ready for Review' && <Button size="sm" variant="outline" className="h-7"
        onClick={() => actions.move({ id: r.id, key: r.key, rowVersion: r.rowVersion, to: 'In Review', needsReason: false })}>{t('task.startReview')}</Button>)),
      ...w.reviews.deliverables.map(delRow)], t('mywork.noReviews')) },
    { id: 'deliverables', title: t('mywork.deliverables'), count: w.deliverables.length, body: list(w.deliverables.map(delRow), t('mywork.noDeliverables')) },
    { id: 'waiting', title: t('mywork.waiting'), count: waiting.length, body: list(waiting.map((r) => taskRow(r, (
      <span className="w-full pl-2 text-xs text-muted-foreground sm:w-auto">{(r.state?.blockedBy ?? []).map((b, i) => (
        <span key={i} className="mr-2">{b.type === 'manual' ? t('task.manualBlocker', { type: tv(b.name ?? ''), reason: b.reason ?? '' }) : `${b.key} ${b.name}`}{b.ownerId && ` · ${w.people[b.ownerId] ?? ''}`}</span>
      ))}</span>))), t('mywork.noWaiting')) },
    { id: 'blocking', title: t('mywork.blocking'), count: blocking.length, body: list(blocking.map((r) => taskRow(r, (
      <span className="w-full pl-2 text-xs text-bad sm:w-auto">{r.state!.blockingTaskIds.map((id) => w.successors.find((x) => x.id === id)).filter(Boolean).map((x) => `${x!.key} ${x!.name}${x!.assigneeName ? ` · ${x!.assigneeName}` : ''}`).join('; ')}</span>))), t('mywork.noBlocking')) },
    { id: 'decisions', title: t('mywork.decisions'), count: w.decisions.length, body: list(w.decisions.map((d) => (
      <li key={d.id} className="flex flex-wrap items-center gap-x-3 px-4 py-2 text-sm">
        <Key>{d.projectNumber}</Key><button className="hover:underline" onClick={() => openPanel('Decision', d.id)}><Key>{d.key}</Key> <span className="font-medium">{d.subject}</span></button>
        <span className="text-xs text-muted-foreground">{t(`mywork.role.${d.role}`)}</span>
        <span className={cn('ml-auto', d.isOverdue && 'font-medium text-bad', d.isDueSoon && 'text-warn')}>{t('wc.requiredBy', { date: fmtDate(d.requiredByDate) })}</span><StatusPill status={d.status} />
      </li>)), t('mywork.noDecisions')) },
    { id: 'actions', title: t('mywork.actions'), count: w.actions.length, body: list(w.actions.map((a) => ( // MTG-01
      <li key={a.id} className={cn('flex flex-wrap items-center gap-x-3 px-4 py-2 text-sm', a.isOverdue && 'bg-bad-bg/30')}>
        <Key>{a.projectNumber}</Key><button className="min-w-0 flex-1 text-left hover:underline" onClick={() => openPanel('Action', a.id)}><Key>{a.key}</Key> <span className="font-medium">{a.text}</span></button>
        {a.ownerType !== 'User' && <ActionOwner a={a} />}
        <span className="text-xs text-muted-foreground">{a.meetingTitle}</span>
        <span className={cn('tabular-nums', a.isOverdue && 'font-medium text-bad')}>{fmtDate(a.dueDate)}</span><StatusPill status={a.status} />
      </li>)), t('mywork.noActions')) },
    { id: 'projects', title: t('mywork.projects'), count: w.projects.length, body: list(w.projects.map((p) => (
      <li key={p.id} className="flex flex-wrap items-center gap-x-3 gap-y-1 px-4 py-2 text-sm">
        <Link className="min-w-0 flex-1 truncate hover:underline" to={`/projects/${p.projectNumber}`}><Key>{p.projectNumber}</Key> <span className="font-medium">{p.name}</span></Link>
        <span className="flex flex-wrap gap-1">{p.roles.map((r) => <span key={r} className="rounded bg-muted px-1.5 text-xs">{t(`role.${r}`)}</span>)}</span>
        <HealthPill health={p.reportedHealth} />
        <span className="text-xs text-muted-foreground">{p.nextMilestone ? `◆ ${p.nextMilestone.name} ${relative(p.nextMilestone.date)}` : ''}</span>
        {!ro && <span className="w-40"><FollowLevelSelect projectId={p.id} level={p.follow?.level} onChanged={reload} /></span>}
      </li>)), t('mywork.noProjects')) },
    { id: 'milestones', title: t('mywork.milestones'), count: w.milestones.length, body: list(w.milestones.map((m) => (
      <li key={m.id} className="flex flex-wrap items-center gap-x-3 px-4 py-2 text-sm">
        <Key>{m.projectNumber}</Key><Key>{m.key}</Key><span className="flex-1 font-medium">{m.name}</span><span>{fmtDate(m.date)} ({relative(m.date)})</span><StatusPill status={m.status} />
      </li>)), t('mywork.noMilestones')) },
    { id: 'completed', title: t('mywork.completed'), count: w.completed.length, open: false, body: list(w.completed.map((r) => taskRow(r)), t('mywork.noCompleted')) },
  ]

  return (
    <Page title={ro ? t('mywork.of', { name: w.person.displayName }) : t('mywork.title')} subtitle={ro ? t('mywork.readOnly') : me.displayName}
      actions={!ro && <><ViewMenu listType="mywork" extra={() => scope.params} />{me.capabilities.createTask && <Button onClick={() => setCreating(true)}><Plus className="size-4" />{t('task.new')}</Button>}</>}>
      {!ro && <MyWorkTabs />}
      <div className="flex flex-wrap items-center gap-2 text-sm">
        <select className="h-8 rounded-md border bg-card px-2" value={f.project} onChange={(e) => set('project', e.target.value || undefined)} aria-label={t('nav.projects')}>
          <option value="">{t('notif.anyProject')}</option>{projectOptions.map(([id, num]) => <option key={id} value={id}>{num}</option>)}
        </select>
        <select className="h-8 rounded-md border bg-card px-2" value={f.discipline} onChange={(e) => set('discipline', e.target.value || undefined)} aria-label={t('common.discipline')}>
          <option value="">{t('projects.anyDiscipline')}</option>{disciplineOptions.map((d) => <option key={d} value={d}>{d}</option>)}
        </select>
        <select className="h-8 rounded-md border bg-card px-2" value={f.status} onChange={(e) => set('status', e.target.value || undefined)} aria-label={t('common.status')}>
          <option value="">{t('task.anyOpenStatus')}</option>{TASK_STATUSES.filter((x) => x !== 'Complete' && x !== 'Cancelled').map((x) => <option key={x} value={x}>{tv(x)}</option>)}
        </select>
        <select className="h-8 rounded-md border bg-card px-2" value={f.priority} onChange={(e) => set('priority', e.target.value || undefined)} aria-label={t('common.priority')}>
          <option value="">{t('task.anyPriority')}</option>{PRIORITIES.map((x) => <option key={x} value={x}>{tv(x)}</option>)}
        </select>
        <label className="text-xs text-muted-foreground">{t('common.from')} <Input type="date" className="inline-flex h-8 w-36" value={f.from} onChange={(e) => set('from', e.target.value || undefined)} /></label>
        <label className="text-xs text-muted-foreground">{t('common.to')} <Input type="date" className="inline-flex h-8 w-36" value={f.to} onChange={(e) => set('to', e.target.value || undefined)} /></label>
        <label className="flex items-center gap-1.5"><input type="checkbox" checked={f.hideWaiting} onChange={(e) => set('hideWaiting', e.target.checked ? '1' : undefined)} />{t('mywork.hideWaiting')}</label>
        <div className="flex-1" />
        <select className="h-8 rounded-md border bg-card px-2" value={f.sort} onChange={(e) => set('sort', e.target.value === 'due' ? undefined : e.target.value)} aria-label={t('mywork.sort')}>
          {SORTS.map((x) => <option key={x} value={x}>{t(`mywork.sort.${x}`)}</option>)}
        </select>
      </div>
      <nav aria-label={t('mywork.sections')} className="flex flex-wrap gap-1.5 text-xs">
        {sections.map((s) => <a key={s.id} href={`#mw-${s.id}`} className="rounded-full border bg-card px-2.5 py-0.5 hover:bg-muted">{s.title} {s.count > 0 && <span className="text-muted-foreground">{s.count}</span>}</a>)}
      </nav>
      {sections.map((s) => s.open === false
        ? <details key={s.id} id={`mw-${s.id}`} className="rounded-lg border bg-card"><summary className="cursor-pointer px-4 py-2.5 text-sm font-semibold">{s.title} <span className="font-normal text-muted-foreground">{s.count}</span></summary>{s.body}</details>
        : <Section key={s.id} id={`mw-${s.id}`} title={s.title} count={s.count}>{s.body}</Section>)}
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
      <div className="flex flex-wrap items-center gap-2 text-sm">
        <div className="inline-flex overflow-hidden rounded-md border bg-card" role="group" aria-label={t('mywork.show')}>
          {WHO.map((x) => <button key={x} type="button" aria-pressed={who === x} onClick={() => set('who', x === 'mine' ? null : x)}
            className={cn('h-8 px-3', who === x ? 'bg-accent font-medium' : 'hover:bg-muted')}>{t(`mywork.who.${x}`)}</button>)}
        </div>
        <span className="text-xs text-muted-foreground">{t(`mywork.who.${who}.about`)}</span>
        {tab === 'completed' && <>
          <div className="flex-1" />
          <label className="text-xs text-muted-foreground">{t('common.from')} <Input type="date" className="inline-flex h-8 w-36" value={from} onChange={(e) => set('from', e.target.value || null)} /></label>
          <label className="text-xs text-muted-foreground">{t('common.to')} <Input type="date" className="inline-flex h-8 w-36" value={to} onChange={(e) => set('to', e.target.value || null)} /></label>
        </>}
      </div>
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      <Section title={t(`mywork.tab.${tab}`)} count={q.data?.totalCount}>
        {q.isPending ? <Loading rows={5} /> : rows.length === 0 ? <Empty>{t(`mywork.empty.${tab}`)}</Empty> : (
          <ul className="divide-y">
            {rows.map((r) => (
              <li key={r.id} className="flex flex-wrap items-center gap-x-3 gap-y-1 px-4 py-2 text-sm">
                <Checkbox checked={r.status === 'Complete'} disabled={r.status === 'Complete' || r.status === 'Cancelled'} aria-label={t('mywork.complete', { key: r.key, name: r.name })}
                  onCheckedChange={() => lane.onDrop(r, 'Done', () => undefined)} />
                <button type="button" className="hover:underline" onClick={() => openPanel('Task', r.id)}><Key>{r.key}</Key></button>
                <button type="button" className="min-w-[10rem] flex-1 text-left font-medium hover:underline" onClick={() => openPanel('Task', r.id)}>{r.name}</button>
                <span title={r.projectName} className="key font-mono text-xs text-muted-foreground">{r.projectNumber}</span>
                <span className={cn('tabular-nums', r.state?.isOverdue && 'font-medium text-bad')}>{tab === 'completed' ? fmtDate(r.completedAt?.slice(0, 10)) : fmtDate(r.dueDate)}</span>
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
      actions={<><Button variant="outline" size="sm" disabled={rows.length === 0} onClick={() => markRead({ ids: rows.map((n) => n.id) })}>{t('mywork.markAllRead')}</Button>
        <Button asChild variant="ghost" size="sm"><Link to="/notifications">{t('mywork.allNotifications')}</Link></Button></>}>
      <MyWorkTabs />
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      <Section title={t('mywork.tab.inbox')} count={rows.length}>
        {q.isPending ? <Loading rows={5} /> : rows.length === 0 ? <Empty>{t('mywork.empty.inbox')}</Empty>
          : <ul className="divide-y">{rows.map((n) => <li key={n.id}><NotificationItem n={n} onOpen={open} /></li>)}</ul>}
      </Section>
      {q.data && q.data.totalCount > q.data.items.length && <p className="text-xs text-muted-foreground">{plural(q.data.totalCount, 'mywork.moreUnreadOne', 'mywork.moreUnread', { n: q.data.totalCount })}</p>}
    </Page>
  )
}
