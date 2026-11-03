import { PersonPanelTrigger } from '@/components/hub/person-panel-trigger'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, CheckCheck, ChevronDown, ChevronRight, ClipboardCopy, ListPlus, MessageSquare, MonitorPlay, MoreHorizontal, Plus, Printer } from 'lucide-react'
import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { useLocation, useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { AttentionPanel } from '@/components/hub/attention'
import { ErrorBanner, Loading, Notice, Page, PageHelp, selectCls } from '@/components/hub/common'
import { InlineDate } from '@/components/hub/fields'
import { useItemPanel } from '@/components/hub/panel-host'
import { ViewMenu } from '@/components/hub/views'
import { Avatar, PeoplePicker } from '@/components/hub/people'
import { Chip, HealthPill, Key, PriorityBadge, StatusPill } from '@/components/hub/pills'
import { Button } from '@/components/ui/button'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import type { components } from '@/gen/api'
import { typedRequest } from '@/lib/api'
import { captureRecordContextOrigin } from '@/lib/record-context-navigation'
import { useFlag, useMe } from '@/lib/auth'
import { invalidateChange } from '@/lib/keys'
import { fmtDate, fmtTime, relative, today } from '@/lib/format'
import { t, tv , runtimeText } from '@/lib/i18n'
import { cn, type Accent } from '@/lib/utils'
import { RecordDecisionDialog } from './Decisions'
import type { DeliverableRow } from './Deliverables'
import { ActionForm, ActionOwner, type ActionRow } from './Meetings'
import { useCurrentProject } from './ProjectLayout'
import { CreateTask, StatusMenu, TaskIndicators, blockersOf, useProjectLists, useTaskActions, useTaskHints, type Blocker, type TaskRow } from './Tasks'
import { DisciplineCoordinationView } from './DisciplineCoordinationView'
import { RecordContext } from '@/components/hub/record-context'
import { readinessCheckLabel, WorkInputFact } from './WorkInputs'
import { readinessSourceGroups, type ReadinessSourceGroup } from './work-readiness-presentation'
import { useWeeklyReadinessAgenda, WeeklyReadinessAgendaView, weeklyReadinessSummary } from './weekly-readiness-agenda'

type Weekly = components['schemas']['WeeklyCoordination']
/** The agenda's own shapes come from the contract; the task, deliverable and action rows keep the page types of the files that own them until those conversions land. */
interface Coord extends Omit<Weekly, 'deliverables' | 'blocked' | 'overdue' | 'dueThisWeek' | 'disciplines' | 'waiting' | 'completed' | 'upcoming' | 'held'> {
  deliverables: DeliverableRow[]; blocked: TaskRow[]; overdue: TaskRow[]; dueThisWeek: TaskRow[]; waiting: ActionRow[]
  disciplines: { discipline: components['schemas']['DashboardDiscipline']; top: TaskRow[] }[]
  completed: { tasks: TaskRow[]; deliverables: DeliverableRow[]; decisions: Weekly['completed']['decisions'] }
  upcoming: { tasks: TaskRow[]; deliverables: DeliverableRow[] }
  held: { tasks: TaskRow[]; deliverables: DeliverableRow[] }
}

/** Blocked work grouped by what blocks it, so the meeting talks about each cause once (§12.13 section 6, AC-WC-02). */
type ContextMap = ReadonlyMap<string, components['schemas']['RecordContextView']>
type BlockerGroup = { key: string; label: string; blocker: Blocker; input?: components['schemas']['RecordContextInputView']; rows: TaskRow[] }
type InputPresentation = { groups: BlockerGroup[]; blockedKeys: ReadonlySet<string> }
function byBlocker(rows: TaskRow[], contexts?: ContextMap): BlockerGroup[] {
  if (contexts) {
    const groups = new Map<string, BlockerGroup>()
    const add = (key: string, row: TaskRow, label: string, blocker: Blocker, input?: components['schemas']['RecordContextInputView']) => {
      const group = groups.get(key) ?? { key, label, blocker, input, rows: [] }
      if (!group.rows.some(task => task.id === row.id)) group.rows.push(row)
      groups.set(key, group)
    }
    for (const row of rows) {
      const context = contexts.get(`Task:${row.id}`)
      for (const input of context?.inputs ?? []) add(`${input.kind}:${input.id}`, row, `${input.key} ${input.title}`,
        { type: input.kind, id: input.id, key: input.key, name: input.title, overdue: false, blocking: true }, input)
      if (row.manualBlockType) add(`manual:${row.id}`, row, t('task.manualBlocker', { type: tv(row.manualBlockType), reason: row.manualBlockReason ?? '' }),
        { type: 'manual', name: row.manualBlockType, reason: row.manualBlockReason, overdue: false, blocking: true })
      if (!context || (context.unavailableInputCount ?? 0) > (row.manualBlockType ? 1 : 0)
        || (context.inputs.length === 0 && !row.manualBlockType))
        add(`unavailable:${row.id}`, row, t('workInputs.unavailable'), { type: 'unavailable', overdue: false, blocking: true })
    }
    return [...groups.values()].sort((a, b) => (a.input?.kind ?? a.blocker.type).localeCompare(b.input?.kind ?? b.blocker.type)
      || (a.input?.key ?? a.label).localeCompare(b.input?.key ?? b.label) || a.key.localeCompare(b.key))
  }
  const groups = new Map<string, BlockerGroup>()
  for (const r of rows)
    for (const b of blockersOf(r.state).filter((x) => x.blocking)) {
      const id = b.type === 'manual' ? `manual:${b.name}:${b.reason}` : `${b.type}:${b.id}`
      const label = b.type === 'manual' ? t('task.manualBlocker', { type: tv(b.name ?? ''), reason: b.reason ?? '' }) : `${b.key} ${b.name}`
      const g = groups.get(id) ?? { key: id, label, blocker: b, rows: [] }
      g.rows.push(r)
      groups.set(id, g)
    }
  return [...groups.values()].sort((a, b) => Math.max(...b.rows.map((r) => r.state?.daysBlocked ?? 0)) - Math.max(...a.rows.map((r) => r.state?.daysBlocked ?? 0)))
}

/** One canonical source membership across every included card, before deciding its placement. */
function inputGroups(rows: TaskRow[], contexts: ContextMap) {
  return byBlocker(rows.filter(task => {
    const context = contexts.get(`Task:${task.id}`)
    return !context?.terminal && (task.state?.isBlocked || task.state?.isWaiting || (context?.inputs.length ?? 0) > 0 || (context?.unavailableInputCount ?? 0) > 0)
  }), contexts)
    .map(group => ({ ...group, rows: [...group.rows].sort((a, b) => a.key.localeCompare(b.key) || a.id.localeCompare(b.id)) }))
}
const inputTaskState = (task: TaskRow) => task.state?.isBlocked ? t('ind.blocked') : task.state?.isWaiting ? t('ind.waiting') : tv(task.status)

function byDiscipline<T extends { disciplineName?: string | null; disciplineOrder?: number }>(rows: T[]) {
  const m = new Map<string, T[]>()
  for (const r of [...rows].sort((a, b) => (a.disciplineOrder ?? 0) - (b.disciplineOrder ?? 0))) m.set(r.disciplineName ?? '', [...(m.get(r.disciplineName ?? '') ?? []), r])
  return [...m.entries()]
}

/** The accountable person with their identity mark; nobody named reads as Unassigned, never as a blank. */
function Who({ id, name, focusKey }: { id?: string | null; name?: string | null; focusKey: string }) {
  return name ? <span className="inline-flex items-center gap-2"><Avatar id={id} name={name} />{id ? <PersonPanelTrigger userId={id} name={name} focusKey={focusKey} /> : <span>{name}</span>}</span> : <span className="text-muted-foreground">{t('ind.unassigned')}</span>
}

// A context section keeps one header accent every week (design §3); urgent sections stay plain so the accent never reads as urgency.
const SECTION_ACCENT: Record<string, Accent> = { headline: 'blue', attention: 'peach', round: 'mint' }
const badge = 'rounded-md bg-secondary px-2 py-0.5 text-xs font-medium text-muted-foreground tabular-nums'
const rowCls = 'flex min-h-(--row-min) flex-wrap items-center gap-x-3 gap-y-1 px-5 py-(--cell-py) [&>.flex-1]:basis-full sm:[&>.flex-1]:basis-auto'

/** Weekly Coordination (§12.13, §13.9): the meeting agenda computed from live data, with meeting mode. */
export function CoordinationTab() {
  const p = useCurrentProject()
  const [sp, setSp] = useSearchParams()
  const qc = useQueryClient()
  const openPanel = useItemPanel()
  const location = useLocation(), contextOn = useFlag('screen_patterns'), peopleOn = useFlag('people_screens'), me = useMe()
  const hints = useTaskHints(p)
  const lists = useProjectLists(p.id)
  const disciplineId = sp.get('discipline') ?? undefined
  const meeting = sp.get('meeting') === '1'
  const set = (k: string, v?: string) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); setSp(n, { replace: true }) }
  const q = useQuery({ queryKey: ['p', p.id, 'coordination', disciplineId], queryFn: async () => (await typedRequest('get', '/api/v1/projects/{id}/coordination', { params: { id: p.id }, query: { disciplineId } })) as unknown as Coord })
  const readinessAgenda = useWeeklyReadinessAgenda(p.id, q.data?.window.thisWeek, disciplineId, !q.isError)
  const contextMap = useMemo(() => new Map((q.data?.contexts ?? []).map(item => [`${item.kind}:${item.id}`, item.context] as const)), [q.data?.contexts])
  const contextFor = (kind: string, id?: string | null) => {
    const value = id ? contextMap.get(`${kind}:${id}`) : undefined
    // Visible cause groups replace only their own successful input facts.
    // Unknown/unavailable facts remain local to each actual task card.
    return contextOn && value ? <div className="w-full"><RecordContext context={kind === 'Task' ? { ...value, inputs: value.inputs.filter(input => !representedInputs.has(`${input.kind}:${input.id}`)) } : value} projectNumber={p.projectNumber} meId={me.id} openPanel={openPanel} /></div> : null
  }
  const openRecord = (type: string, id: string, source?: Element | null) => {
    openPanel(type, id, contextOn ? captureRecordContextOrigin(location, source) : undefined)
  }
  const [log, setLog] = useState<{ id: string; key: string; what: string; at: string }[]>([])
  const [hideDiscussed, setHideDiscussed] = useState(false)
  const [printAll, setPrintAll] = useState(false)
  // Look-back, look-ahead and held items start collapsed (§41: secondary sections one click away); print shows them all.
  const [collapsed, setCollapsed] = useState<Set<string>>(() => new Set(['completed', 'upcoming', 'held']))
  const [creating, setCreating] = useState(false)
  const [recording, setRecording] = useState<{ id: string; key: string } | null>(null)
  const [capturing, setCapturing] = useState<{ taskId?: string; decisionId?: string; label?: string; ownerId?: string; ownerName?: string;
    links?: { targetType: string; targetId: string }[]; linkedActionIds?: string[] } | null>(null)
  const reload = () => { q.refetch(); invalidateChange(qc, 'Task', p.id) }
  const actions = useTaskActions(reload, (id, key, what) => setLog((l) => [...l, { id, key, what, at: new Date().toISOString() }]))
  const touched = useMemo(() => new Set(log.map((x) => x.id)), [log])
  const root = useRef<HTMLDivElement>(null)

  // Meeting mode steps through section headers with the arrow keys (AC-WC-03).
  useEffect(() => {
    if (!meeting) return
    const onKey = (e: KeyboardEvent) => {
      if (e.key !== 'ArrowRight' && e.key !== 'ArrowLeft') return
      const el = e.target instanceof HTMLElement ? e.target : null
      if (el && (['INPUT', 'TEXTAREA', 'SELECT'].includes(el.tagName) || el.isContentEditable || el.closest('[role=dialog],[role=menu],[role=listbox]'))) return
      const heads = [...(root.current?.querySelectorAll<HTMLElement>('[data-section-head]') ?? [])]
      if (!heads.length) return
      e.preventDefault()
      const i = heads.indexOf(document.activeElement as HTMLElement)
      const next = heads[e.key === 'ArrowRight' ? Math.min(i + 1, heads.length - 1) : Math.max(i - 1, 0)]
      next.focus()
      next.scrollIntoView({ block: 'start', behavior: 'smooth' })
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [meeting])

  // Meeting mode is projected: body, metadata and controls step up one size so they read across a room (§13.9).
  const scale = meeting ? 'text-base [&_.text-sm]:text-base [&_.text-xs]:text-sm' : undefined
  if (q.isPending) return <Page title={t('ptab.coordination')} className={scale}><div className="rounded-lg border bg-card"><Loading rows={10} /></div></Page>
  if (q.error) return <Page title={t('ptab.coordination')} className={scale}><ErrorBanner error={q.error} retry={() => q.refetch()} /></Page>
  const c = q.data
  const visible = (rows: TaskRow[]) => (hideDiscussed ? rows.filter((r) => !touched.has(r.id)) : rows)
  const secondaryRows = (rows: TaskRow[]) => contextOn ? visible(rows) : rows

  const canCapture = c.window.canMarkReviewed // MTG-04: the PM and leads capture actions in the meeting
  const markReviewed = async () => { await typedRequest('post', '/api/v1/projects/{id}/coordination/reviewed', { params: { id: p.id } }); toast.success(t('wc.reviewedToast')); reload() }
  const copySummary = async () => {
    await navigator.clipboard.writeText(summaryText(p.projectNumber, p.name, c, contextOn ? contextMap : undefined, contextOn ? inputPresentation : undefined, contextOn ? readinessGroups : undefined)
      + (readinessAgenda.enabled ? `\n\n${weeklyReadinessSummary(readinessAgenda)}` : ''))
    toast.success(t('wc.copied'))
  }

  const row = (r: TaskRow, extra?: ReactNode, surface = 'tasks') => (
    <li key={r.id} className={cn(rowCls, touched.has(r.id) && 'bg-ok-bg/50 shadow-[inset_3px_0_0_var(--ok)]')}>
      {contextFor('Task', r.id)}<button className="hover:underline" onClick={e => openRecord('Task', r.id, e.currentTarget)}><Key>{r.key}</Key></button>
      <button className="min-w-[10rem] flex-1 text-left font-medium hover:underline" onClick={e => openRecord('Task', r.id, e.currentTarget)}>{r.name}</button>
      {extra}
      {meeting && hints.manages(r) ? (
        <span className="w-48"><PeoplePicker compact value={r.assigneeId} valueName={r.assigneeName} placeholder={t('ind.unassigned')} label={t('task.assigneeOf', { key: r.key })} onChange={(id) => actions.save(r, { assigneeId: id })} />{peopleOn && r.assigneeId && r.assigneeName && <PersonPanelTrigger userId={r.assigneeId} name={r.assigneeName} focusKey={`coordination:${surface}:task:${r.id}:assignee-details`}>{t('common.details')}</PersonPanelTrigger>}</span>
      ) : <Who id={r.assigneeId} name={r.assigneeName} focusKey={`coordination:${surface}:task:${r.id}:assignee`} />}
      {meeting && hints.due(r)
        ? <span className="w-36"><InlineDate value={r.dueDate} title={t('task.dueOf', { key: r.key })} onSave={(v) => actions.save(r, { dueDate: v })} /></span>
        : <span className={cn('tabular-nums', r.state?.isOverdue && 'font-medium text-bad')}>{fmtDate(r.dueDate)}</span>}
      <StatusMenu r={r} onMove={actions.move} disabled={!meeting && false} />
      <PriorityBadge priority={r.priority === 'High' || r.priority === 'Critical' ? r.priority : null} />
      <TaskIndicators r={r} />
      {meeting && <Button variant="ghost" size="icon-sm" aria-label={t('wc.commentOn', { key: r.key })} onClick={e => openRecord('Task', r.id, e.currentTarget)}><MessageSquare className="size-4" /></Button>}
      {meeting && canCapture && <Button variant="ghost" size="icon-sm" aria-label={t('wc.actionOn', { key: r.key })}
        onClick={() => setCapturing({ taskId: r.id, label: `${r.key} ${r.name}`, ownerId: r.assigneeId ?? undefined, ownerName: r.assigneeName ?? undefined })}><ListPlus className="size-4" /></Button>}
    </li>
  )
  const delRow = (d: DeliverableRow, extra?: ReactNode, surface = 'deliverables') => (
    <li key={d.id} className={rowCls}>
      {contextFor('Deliverable', d.id)}<button className="hover:underline" onClick={e => openRecord('Deliverable', d.id, e.currentTarget)}><Key>{d.key}</Key></button>
      <button className="min-w-0 flex-1 text-left font-medium hover:underline" onClick={e => openRecord('Deliverable', d.id, e.currentTarget)}>{d.name}</button>
      {extra}
      <Who id={d.ownerId} name={d.ownerName} focusKey={`coordination:${surface}:deliverable:${d.id}:owner`} />
      <span className={cn('tabular-nums', d.state?.isOverdue && 'font-medium text-bad')}>{fmtDate(d.dueDate)}</span>
      <StatusPill status={d.status} />
    </li>
  )
  const list = (items: ReactNode[]) => items.length ? <ul className="divide-y">{items}</ul> : <p className="px-5 py-3 text-muted-foreground">{t('wc.nothing')}</p>
  const grouped = (groups: [string, ReactNode[]][]) => groups.length === 0 ? list([]) : groups.map(([g, items]) => (
    <div key={g}><div className="border-b bg-muted px-5 py-1.5 text-xs font-semibold text-muted-foreground">{g}</div><ul className="divide-y">{items}</ul></div>
  ))
  const tally = (n: number, text: string, tone: 'bad' | 'warn') => <Chip tone={n > 0 ? tone : 'idle'}><span className="tabular-nums">{text}</span></Chip>

  const blockedGroups = byBlocker(visible(c.blocked), contextOn ? contextMap : undefined)
  const shown = (section: string) => !collapsed.has(section) || printAll
  const blockedPlacement = new Set(shown('blocked') ? blockedGroups.map(group => group.key) : [])
  // A repeated task in Due/Upcoming/discipline round is one affected task.
  // Use the same visible/discussed row filters as the actual rendered cards.
  // These groups never change the server's Blocked membership or figures.
  const shownTasks = [
    ...(shown('blocked') ? visible(c.blocked) : []),
    ...(shown('overdue') ? visible(c.overdue) : []),
    ...(shown('thisWeek') ? visible(c.dueThisWeek) : []),
    ...(shown('round') ? secondaryRows(c.disciplines.flatMap(discipline => discipline.top)) : []),
    ...(shown('upcoming') ? secondaryRows(c.upcoming.tasks) : []),
    ...(shown('held') ? secondaryRows(c.held.tasks) : []),
  ]
  const inputPresentation: InputPresentation = { groups: contextOn ? inputGroups(shownTasks, contextMap) : [], blockedKeys: blockedPlacement }
  const membership = new Map(inputPresentation.groups.map(group => [group.key, group]))
  const waitingGroups = inputPresentation.groups.filter(group => group.input && !blockedPlacement.has(group.key))
  const representedInputs = new Set(inputPresentation.groups.filter(group => group.input).map(group => group.key))
  const readinessGroups = contextOn ? readinessSourceGroups(shownTasks, c.readinessInputs ?? []) : []
  const sourceFacts = (group: BlockerGroup) => <>
    <WorkInputFact input={group.input!} number={p.projectNumber} meId={me.id} focusKey={`coordination-input:${group.input!.kind}:${group.input!.id}:source`} />
    <p className="text-xs text-muted-foreground">{t('wc.tasksN', { n: group.rows.length })}</p>
    <ul aria-label={t('workInputs.otherTasks')} className="space-y-1 text-sm">{group.rows.map(task => <li key={task.id} className="flex flex-wrap items-center gap-2">
      <button type="button" className="text-left text-primary underline underline-offset-2" onClick={event => openRecord('Task', task.id, event.currentTarget)}><Key>{task.key}</Key> · {task.name}</button>
      <Chip tone={task.state?.isBlocked ? 'bad' : task.state?.isWaiting ? 'warn' : 'idle'}>{inputTaskState(task)}</Chip>
    </li>)}</ul>
  </>
  const sections: { id: string; title: string; count: number; body: ReactNode; fixedOpen?: boolean }[] = [
    { id: 'headline', title: t('wc.s.headline'), count: 0, body: (
      <div className="flex flex-wrap items-center gap-x-6 gap-y-3 px-5 py-4">
        <span className="flex flex-wrap items-center gap-2">
          <HealthPill health={c.headline.computedHealth ?? p.health.computed} label={t('health.computed')} />
          {p.health.overrideActive && <HealthPill health={p.health.reported} label={t('health.reported')} />}
        </span>
        {c.headline.nextMilestone && <>{contextFor('Milestone', c.headline.nextMilestone.id)}<span><span aria-hidden>◆ </span>{c.headline.nextMilestone.name} · <span className="tabular-nums">{fmtDate(c.headline.nextMilestone.date)}</span> ({relative(c.headline.nextMilestone.date)})</span></>}
        {p.nextSubmission && <span>{t('dash.nextSubmission')}: {p.nextSubmission.name} · <span className="tabular-nums">{fmtDate(p.nextSubmission.date)}</span></span>}
        <span className="flex flex-wrap items-center gap-2">
          {tally(c.headline.overdueTasks.value, t('wc.overdueN', { n: c.headline.overdueTasks.value }), 'bad')}{tally(c.headline.blockedTasks.value, t('wc.blockedN', { n: c.headline.blockedTasks.value }), 'bad')}
          {tally(c.headline.decisionsOverdue, t('wc.decisionsOverdueN', { n: c.headline.decisionsOverdue }), 'bad')}{tally(c.headline.deliverablesAtRisk.value, t('wc.atRiskN', { n: c.headline.deliverablesAtRisk.value }), 'warn')}
        </span>
      </div>) },
    { id: 'attention', title: t('wc.s.attention'), count: -1, body: <AttentionPanel projectId={p.id} canSnooze={c.window.canMarkReviewed} disciplineId={disciplineId} severity="Critical,Warning" /> },
    { id: 'milestones', title: t('wc.s.milestones'), count: c.milestones.length, body: list(c.milestones.map((m) => (
      <li key={m.id} className={rowCls}>
        {contextFor('Milestone', m.id)}<button className="text-left hover:underline" onClick={e => openRecord('Milestone', m.id, e.currentTarget)}><Key>{m.key}</Key> <span className="font-medium">{m.name}</span></button>
        <span className="tabular-nums">{fmtDate(m.date)} ({relative(m.date)})</span><StatusPill status={m.status} />
        <span className="text-muted-foreground tabular-nums">{t('wc.prereqs', { issued: m.deliverableIssued, total: m.deliverableTotal, done: m.taskComplete, tasks: m.taskTotal })}</span>
      </li>))) },
    { id: 'deliverables', title: t('wc.s.deliverables'), count: c.deliverables.length, body: grouped(byDiscipline(c.deliverables).map(([g, rows]) => [g, rows.map((d) => delRow(d))])) },
    { id: 'decisions', title: t('wc.s.decisions'), count: c.decisions.length, body: list(c.decisions.map((d) => (
      <li key={d.id} className={rowCls}>
        {contextFor('Decision', d.id)}<button className="text-left hover:underline" onClick={e => openRecord('Decision', d.id, e.currentTarget)}><Key>{d.key}</Key> <span className="font-medium">{d.subject}</span></button>
        <Who id={d.ownerUserId} name={d.ownerName} focusKey={`coordination:decision:${d.id}:owner`} />
        <span className={cn('tabular-nums', d.isOverdue && 'font-medium text-bad')}>{t('wc.requiredBy', { date: fmtDate(d.requiredByDate) })}{d.isOverdue && ` · ${t('ind.overdueD', { n: d.daysOverdue })}`}</span>
        {d.blocking > 0 && <span className="text-bad"><span aria-hidden>■ </span>{t('ind.blocking', { n: d.blocking })}</span>}<StatusPill status={d.status} />
        {meeting && <Button size="sm" variant="outline" className="no-print ml-auto" onClick={() => setRecording({ id: d.id, key: d.key })}>{t('decision.record')}</Button>}
        {meeting && canCapture && <Button variant="ghost" size="icon-sm" className="no-print" aria-label={t('wc.actionOn', { key: d.key })}
          onClick={() => setCapturing({ decisionId: d.id, label: `${d.key} ${d.subject}` })}><ListPlus className="size-4" /></Button>}
      </li>))) },
    { id: 'blocked', title: t('wc.s.blocked'), count: c.blocked.length, body: blockedGroups.length === 0 ? <p className="px-5 py-3 text-muted-foreground">{t('wc.noneBlocked')}</p> : blockedGroups.map((g) => (
      <div key={g.key}>
        {contextOn && g.input ? <div className="space-y-2 border-b bg-card px-5 py-3">{sourceFacts(membership.get(g.key) ?? g)}</div> : <div className="flex flex-wrap items-center gap-2 border-b bg-bad-bg px-5 py-2 text-sm font-semibold text-bad">
          <span aria-hidden>■</span>{t('wc.blockedBy', { what: g.label })}{g.blocker.overdue && <span className="text-xs font-normal">({t('ind.overdue')})</span>}
          {g.blocker.type === 'task' && g.blocker.id && <button className="text-xs font-normal underline underline-offset-4" onClick={e => openRecord('Task', g.blocker.id!, e.currentTarget)}>{t('wc.openCause')}</button>}
          <span className="ml-auto text-xs font-normal tabular-nums">{t('wc.tasksN', { n: g.rows.length })}</span>
        </div>}
        <ul className="divide-y">{g.rows.map((r) => row(r, <span className="text-xs text-muted-foreground tabular-nums">{t('ind.blockedD', { n: r.state?.daysBlocked ?? 0 })}</span>, `blocked:${g.key}`))}</ul>
      </div>)) },
    ...(waitingGroups.length > 0 ? [{ id: 'waitingInputs', title: t('workInputs.title'), count: waitingGroups.length, fixedOpen: true, body: <ul className="divide-y">
      {waitingGroups.map(group => <li key={group.key} className="space-y-3 px-5 py-3">
        {sourceFacts(group)}
      </li>)}
    </ul> }] : []),
    ...(readinessAgenda.enabled ? [{ id: 'readinessAgenda', title: t('s38.agendaTitle'), count: (readinessAgenda.data?.constraints.totalCount ?? 0) + (readinessAgenda.data?.proposals.totalCount ?? 0), fixedOpen: true,
      body: <WeeklyReadinessAgendaView model={readinessAgenda} projectNumber={p.projectNumber} /> }] : []),
    ...(readinessGroups.length > 0 ? [{ id: 'readinessInputs', title: t('workInputs.readiness'), count: readinessGroups.length, fixedOpen: true, body: <ul className="divide-y">
      {readinessGroups.map(group => <li key={group.key} className="space-y-3 px-5 py-3">
        <WorkInputFact input={group.source} number={p.projectNumber} meId={me.id} focusKey={`coordination-readiness:${group.key}:source:${group.source.kind}:${group.source.id}`} />
        <p className="text-xs text-muted-foreground">{tv(group.status)} · {t('workInputs.readinessLinkedWork', { n: new Set(group.members.map(member => member.task.id)).size })}</p>
        <ul className="space-y-3">{group.members.map(({ task, input, readiness }) => <li key={`${task.id}:${input.source.kind}:${input.source.id}`} className="space-y-2 border-l pl-3 text-sm">
          <button type="button" className="text-left text-primary underline underline-offset-2" onClick={event => openRecord('Task', task.id, event.currentTarget)}><Key>{task.key}</Key> · {task.name}</button>
          <p className="text-xs">{t(input.relationship === 'DirectTarget' ? 'workInputs.directTarget' : 'workInputs.viaConstraintKey', { key: input.source.key })} · {tv(readiness.state)} · {tv(input.code)}: {readinessCheckLabel(input)}</p>
          {input.relationship === 'Constraint' && <><WorkInputFact input={input.source} number={p.projectNumber} meId={me.id} focusKey={`coordination-readiness:${group.key}:task:${task.id}:check:${input.code}:constraint:${input.source.kind}:${input.source.id}`} />
            <p className="text-xs text-muted-foreground">{tv(input.status)} · {t('workInputs.constraintPersists')}</p></>}
        </li>)}</ul>
      </li>)}
    </ul> }] : []),
    { id: 'overdue', title: t('wc.s.overdue'), count: c.overdue.length, body: grouped(byDiscipline([...visible(c.overdue)].sort((a, b) => (b.state?.daysOverdue ?? 0) - (a.state?.daysOverdue ?? 0))).map(([g, rows]) => [g, rows.map((r) => row(r, undefined, 'overdue'))])) },
    { id: 'thisWeek', title: t('wc.s.thisWeek'), count: c.dueThisWeek.length, body: grouped(byDiscipline(visible(c.dueThisWeek)).map(([g, rows]) => [g, rows.map((r) => row(r, undefined, 'this-week'))])) },
    { id: 'round', title: t('wc.s.round'), count: c.disciplines.length, body: (
      <div className="grid gap-4 p-5 lg:grid-cols-2">
        {c.disciplines.map(({ discipline: d, top }) => {
          const lead = p.disciplines.find((x) => x.id === d.disciplineId)?.leadName
          const rows = secondaryRows(top)
          return <div key={d.disciplineId} className="overflow-hidden rounded-lg border">
            <div className="flex flex-wrap items-center gap-2 border-b px-4 py-2.5">
              <span className="font-semibold">{d.name}</span><HealthPill health={d.health} />
              {lead ? <Who id={d.leadId} name={lead} focusKey={`coordination:discipline:${d.disciplineId}:lead`} /> : <span className="text-muted-foreground">{t('dash.noLead')}</span>}
              <span className="ml-auto text-xs text-muted-foreground tabular-nums">{t('wc.roundCounts', { open: d.open, overdue: d.overdue, blocked: d.blocked, due: d.deliverablesDue14 })}</span>
            </div>
            <ul className="divide-y text-sm">{rows.length === 0 ? <li className="px-5 py-3 text-muted-foreground">{t('wc.nothing')}</li> : rows.map((r) => row(r, undefined, `round:${d.disciplineId}`))}</ul>
          </div>
        })}
      </div>) },
    ...(c.issues.length + c.risks.length > 0 ? [{ id: 'issues', title: t('wc.s.issues'), count: c.issues.length + c.risks.length, body: list([
      ...c.issues.map((i) => <li key={i.id} className={rowCls}>{contextFor('Issue', i.id)}<button className="hover:underline" onClick={e => openRecord('Issue', i.id, e.currentTarget)}><Key>{i.key}</Key></button>
        <button className="flex-1 text-left font-medium hover:underline" onClick={e => openRecord('Issue', i.id, e.currentTarget)}>{i.title}</button><StatusPill status={i.severity === 'High' ? 'Critical' : i.status} /></li>),
      ...c.risks.map((r) => <li key={r.id} className={rowCls}>{contextFor('Risk', r.id)}<button className="hover:underline" onClick={e => openRecord('Risk', r.id, e.currentTarget)}><Key>{r.key}</Key></button>
        <button className="flex-1 text-left font-medium hover:underline" onClick={e => openRecord('Risk', r.id, e.currentTarget)}>{r.title}</button><span className="text-bad tabular-nums"><span aria-hidden>■ </span>{t('wc.riskScore', { n: r.probability * r.impact })}</span></li>),
    ]) }] : []),
    ...(c.waiting.length > 0 ? [{ id: 'waiting', title: t('wc.s.waiting'), count: c.waiting.length, body: list(c.waiting.map((a) => (
      <li key={a.id} className={cn(rowCls, a.isOverdue && 'bg-bad-bg/40')}>
        {contextFor('Action', a.id)}<button className="hover:underline" onClick={e => openRecord('Action', a.id, e.currentTarget)}><Key>{a.key}</Key></button>
        <button className="min-w-[10rem] flex-1 text-left font-medium hover:underline" onClick={e => openRecord('Action', a.id, e.currentTarget)}>{a.text}</button>
        <ActionOwner a={a} focusKey={`coordination:waiting:${a.id}`} /><span className={cn('tabular-nums', a.isOverdue && 'font-medium text-bad')}>{fmtDate(a.dueDate)}{a.isOverdue && ` · ${t('ind.overdueD', { n: a.daysOverdue })}`}</span>
        <StatusPill status={a.status} />
      </li>))) }] : []),
    { id: 'completed', title: t('wc.s.completed'), count: c.completed.tasks.length + c.completed.deliverables.length + c.completed.decisions.length, body: list([
      ...c.completed.tasks.map((r) => row(r, <span className="text-xs text-ok"><span aria-hidden>✓ </span>{t('wc.completedOn', { date: fmtDate(r.completedAt) })}</span>, 'completed')),
      ...c.completed.deliverables.map((d) => delRow(d, <span className="text-xs text-ok"><span aria-hidden>✓ </span>{t('wc.issuedOn', { date: fmtDate(d.issuedDate) })}</span>, 'completed')),
      ...c.completed.decisions.map((d) => <li key={d.id} className={rowCls}>{contextFor('Decision', d.id)}{contextOn ? <><button className="text-left hover:underline" onClick={e => openRecord('Decision', d.id, e.currentTarget)}><Key>{d.key}</Key></button><button className="flex-1 text-left font-medium hover:underline" onClick={e => openRecord('Decision', d.id, e.currentTarget)}>{d.subject}</button></> : <><Key>{d.key}</Key><span className="flex-1 font-medium">{d.subject}</span></>}<span className="text-xs text-ok"><span aria-hidden>✓ </span>{t('wc.decidedOn', { date: fmtDate(d.decisionDate) })}</span></li>),
    ]) },
    { id: 'upcoming', title: t('wc.s.upcoming'), count: c.upcoming.tasks.length + c.upcoming.deliverables.length, body: list([...secondaryRows(c.upcoming.tasks).map((r) => row(r, undefined, 'upcoming')), ...c.upcoming.deliverables.map((d) => delRow(d, undefined, 'upcoming'))]) },
    { id: 'held', title: t('wc.s.held'), count: c.held.tasks.length + c.held.deliverables.length, body: list([
      ...secondaryRows(c.held.tasks).map((r) => row(r, <span className="text-xs text-muted-foreground tabular-nums">{t('wc.heldDays', { n: daysSince(r.statusChangedAt ?? undefined) })}</span>, 'held')),
      ...c.held.deliverables.map((d) => delRow(d, undefined, 'held')),
    ]) },
  ]

  return (
    <Page title={t('ptab.coordination')} className={scale}
      subtitle={<>{t('wc.window', { from: fmtDate(c.window.thisWeek.from), to: fmtDate(c.window.thisWeek.to) })} ·{' '}
        {c.window.lastCoordinationReviewedAt ? t('wc.sinceReview', { when: fmtTime(c.window.lastCoordinationReviewedAt), who: c.window.reviewedBy ?? '' }) : t('wc.neverReviewedShort')}
        {/* One help for the page (§41 R5): the weekly agenda and the discipline coordination view it opens with. */}
        <PageHelp><p>{t('wc.help')}</p><p className="mt-2">{t('dcv.subtitle')}</p><p className="mt-2">{t('dcv.scopeNote')}</p><p className="mt-2">{t('dcv.allSectionsPage')}</p></PageHelp></>}
      actions={<>
        <ViewMenu listType="coordination" projectId={p.id} />
        {!meeting && (
          <span className="flex items-center gap-2"><label htmlFor="wc-scope" className="text-sm font-medium">{t('dash.scope')}</label>
            <select id="wc-scope" className={cn(selectCls, 'w-auto min-w-44')} value={disciplineId ?? ''} onChange={(e) => set('discipline', e.target.value || undefined)}>
              <option value="">{t('dash.allDisciplines')}</option>{p.disciplines.filter((d) => d.isActive).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select></span>
        )}
        {meeting && <label className="flex min-h-(--control-h) items-center gap-2 text-sm"><input type="checkbox" className="size-4 accent-(--primary)" checked={hideDiscussed} onChange={(e) => setHideDiscussed(e.target.checked)} />{t('wc.hideDiscussed')}</label>}
        {meeting && p.permissions.createTaskIn.length > 0 && <Button variant="outline" onClick={() => setCreating(true)}><Plus className="size-4" />{t('task.new')}</Button>}
        {meeting && canCapture && <Button variant="outline" onClick={() => setCapturing({})}><ListPlus className="size-4" />{t('action.new')}</Button>}
        <Button variant="outline" aria-pressed={meeting} className={cn(meeting && 'border-primary bg-accent font-semibold text-accent-foreground hover:bg-accent')} onClick={() => set('meeting', meeting ? undefined : '1')}>{meeting ? <Check className="size-4" /> : <MonitorPlay className="size-4" />}{t('wc.meetingMode')}</Button>
        <DropdownMenu>
          <DropdownMenuTrigger asChild><Button variant="outline" size="icon" aria-label={t('wc.moreActions')}><MoreHorizontal className="size-5" /></Button></DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuItem disabled={printAll || readinessAgenda.enabled && readinessAgenda.query.isFetching} onSelect={() => setPrintAll(true)}><Printer className="size-4" />{t('wc.print')}</DropdownMenuItem>
            <DropdownMenuItem disabled={readinessAgenda.enabled && readinessAgenda.query.isFetching} onSelect={copySummary}><ClipboardCopy className="size-4" />{t('wc.copySummary')}</DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
        {c.window.canMarkReviewed && <Button onClick={markReviewed}><CheckCheck className="size-4" />{t('wc.markReviewed')}</Button>}
      </>}>
      {p.status !== 'Active' && <Notice title={t('wc.notActive', { status: tv(p.status) })} />}
      <DisciplineCoordinationView printAll={printAll} onPrinted={setPrintAll} project={p} disciplineId={disciplineId} meeting={meeting} canCapture={canCapture}
        onCapture={(label, links, linkedActionIds) => setCapturing({ label, links, linkedActionIds })} />
      <div ref={root} className="grid gap-6 lg:grid-cols-[13rem_1fr]">
        <nav aria-label={t('wc.index')} className="no-print hidden lg:block">
          <ol className="sticky top-14 space-y-0.5 text-sm">
            {sections.map((s, i) => (
              <li key={s.id}><a href={`#wc-${s.id}`} className="flex items-center justify-between gap-2 rounded-md px-2.5 py-1.5 hover:bg-muted"><span>{i + 1}. {s.title}</span>{s.count > 0 && <span className={badge}>{s.count}</span>}</a></li>
            ))}
          </ol>
        </nav>
        <div className="min-w-0 space-y-4">
          {sections.map((s, i) => {
            const shut = !s.fixedOpen && collapsed.has(s.id)
            return (
              <section key={s.id} id={`wc-${s.id}`} className="scroll-mt-14 overflow-hidden rounded-lg border bg-card" aria-labelledby={`wc-h-${s.id}`}>
                <h2 id={`wc-h-${s.id}`} data-section-head tabIndex={-1} data-accent={SECTION_ACCENT[s.id]}
                  className={cn('flex scroll-mt-14 items-center gap-2 px-3 py-2 font-semibold outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary', meeting ? 'text-xl/7' : 'text-base/6', !shut && 'border-b', SECTION_ACCENT[s.id] && 'bg-(--acc-bg)')}>
                  {!s.fixedOpen ? <button className="grid size-8 shrink-0 place-items-center rounded-md hover:bg-muted" aria-expanded={!shut} aria-label={shut ? t('wc.expand') : t('wc.collapse')}
                    onClick={() => setCollapsed((x) => { const n = new Set(x); if (n.has(s.id)) n.delete(s.id); else n.add(s.id); return n })}>
                    {shut ? <ChevronRight className="size-4" /> : <ChevronDown className="size-4" />}
                  </button> : <span aria-hidden className="w-8 shrink-0" />}
                  <span>{i + 1}. {s.title}</span>{s.count > 0 && <span className={badge}>{s.count}</span>}
                </h2>
                <div className={cn('text-sm', shut && 'hidden print:block')}>{s.body}</div>
              </section>
            )
          })}
        </div>
      </div>
      {meeting && log.length > 0 && (
        <aside aria-label={t('wc.tray')} className="no-print fixed bottom-16 right-4 z-30 max-h-64 w-80 overflow-y-auto rounded-xl border bg-card p-4 text-sm shadow-popover md:bottom-4">
          <div className="mb-2 font-semibold">{t('wc.tray')} <span className={badge}>{log.length}</span></div>
          <ul className="space-y-1.5">{log.map((x, i) => <li key={i}><Key>{x.key}</Key> {x.what}</li>)}</ul>
        </aside>
      )}
      {recording && <RecordDecisionDialog id={recording.id} onClose={(ok) => { if (ok) { setLog((l) => [...l, { id: recording.id, key: recording.key, what: t('decision.recordedShort'), at: new Date().toISOString() }]); reload() } setRecording(null) }} />}
      {creating && <CreateTask p={p} deliverables={lists.deliverables} milestones={lists.milestones} defaults={{ projectDisciplineId: disciplineId }}
        onClose={(id) => { setCreating(false); if (id) { setLog((l) => [...l, { id, key: t('wc.newTask'), what: t('wc.created'), at: new Date().toISOString() }]); reload() } }} />}
      {capturing && <ActionForm projectId={p.id} related={capturing.label ? { taskId: capturing.taskId, decisionId: capturing.decisionId, label: capturing.label } : undefined}
        links={capturing.links} linkedActionIds={capturing.linkedActionIds}
        defaultOwner={capturing.ownerId ? { type: 'User', userId: capturing.ownerId, userName: capturing.ownerName } : undefined}
        onClose={(created) => { setCapturing(null); if (created) { setLog((l) => [...l, { id: created.id, key: created.key, what: t(created.reused ? 'dcv.reuseLogged' : 'wc.actionAdded', { text: created.text }), at: new Date().toISOString() }]); reload() } }} />}
      {actions.dialogs}
    </Page>
  )
}

function daysSince(ts?: string) {
  if (!ts) return 0
  return Math.max(0, Math.round((Date.parse(today() + 'T00:00:00Z') - Date.parse(ts.slice(0, 10) + 'T00:00:00Z')) / 86_400_000))
}

/** Plain text of sections 1, 3, 5, 6 and 7 with item keys, for the meeting notes (FR-005, AC-WC-06). */
export function summaryText(number: string, name: string, c: Coord, contexts?: ContextMap, displayedInputs?: InputPresentation, displayedReadiness?: ReadinessSourceGroup[]) {
  const lines = [`${number} ${name} — ${t('ptab.coordination')} ${fmtDate(today())}`, '']
  const h = c.headline
  lines.push(`${t('wc.s.headline')}: ${runtimeText(`health.${h.computedHealth ?? 'Grey'}`)} · ${t('wc.overdueN', { n: h.overdueTasks.value })} · ${t('wc.blockedN', { n: h.blockedTasks.value })} · ${t('wc.decisionsOverdueN', { n: h.decisionsOverdue })} · ${t('wc.atRiskN', { n: h.deliverablesAtRisk.value })}`)
  if (h.nextMilestone) lines.push(`${t('dash.nextMilestone')}: ${h.nextMilestone.key} ${h.nextMilestone.name} ${fmtDate(h.nextMilestone.date)} (${relative(h.nextMilestone.date)})`)
  const block = (title: string, rows: string[]) => { lines.push('', title); lines.push(...(rows.length ? rows.map((r) => `- ${r}`) : [`- ${t('wc.nothing')}`])) }
  block(t('wc.s.milestones'), c.milestones.map((m) => `${m.key} ${m.name} — ${fmtDate(m.date)} (${relative(m.date)}) — ${tv(m.status ?? '')} — ${t('wc.prereqs', { issued: m.deliverableIssued, total: m.deliverableTotal, done: m.taskComplete, tasks: m.taskTotal })}`))
  block(t('wc.s.decisions'), c.decisions.map((d) => `${d.key} ${d.subject} — ${t('wc.requiredBy', { date: fmtDate(d.requiredByDate) })}${d.isOverdue ? ` (${t('ind.overdueD', { n: d.daysOverdue })})` : ''}${d.ownerName ? ` — ${d.ownerName}` : ''}`))
  if (contexts) {
    const presentation = displayedInputs ?? {
      groups: inputGroups([...c.blocked, ...c.dueThisWeek, ...c.overdue, ...c.disciplines.flatMap(discipline => discipline.top), ...c.upcoming.tasks, ...c.held.tasks], contexts),
      blockedKeys: new Set(byBlocker(c.blocked, contexts).map(group => group.key)),
    }
    const members = (group: BlockerGroup) => group.rows.map(row => `${row.key} ${row.name} [${inputTaskState(row)}]`).join('; ')
    block(t('wc.s.blocked'), presentation.groups.filter(group => presentation.blockedKeys.has(group.key))
      .map(group => `${t('wc.blockedBy', { what: group.label })}: ${members(group)}`))
    const waiting = presentation.groups.filter(group => !presentation.blockedKeys.has(group.key))
    if (waiting.length > 0) block(t('workInputs.title'), waiting.map(group => `${group.label}: ${members(group)}`))
    const readiness = displayedReadiness ?? readinessSourceGroups([...c.blocked, ...c.dueThisWeek, ...c.overdue,
      ...c.disciplines.flatMap(discipline => discipline.top), ...c.upcoming.tasks, ...c.held.tasks], c.readinessInputs ?? [])
    if (readiness.length > 0) block(t('workInputs.readiness'), readiness.map(group => `${group.source.key} ${group.source.title} [${tv(group.status)}]: ${group.members.map(member =>
      `${member.task.key} ${member.task.name} [${t(member.input.relationship === 'DirectTarget' ? 'workInputs.directTarget' : 'workInputs.viaConstraintKey', { key: member.input.source.key })}; ${tv(member.input.status)}; ${tv(member.readiness.state)}; ${readinessCheckLabel(member.input)}]`).join('; ')}`))
  } else block(t('wc.s.blocked'), byBlocker(c.blocked).map(group => `${t('wc.blockedBy', { what: group.label })}: ${group.rows.map(row => `${row.key} ${row.name}`).join('; ')}`))
  block(t('wc.s.overdue'), [...c.overdue].sort((a, b) => (b.state?.daysOverdue ?? 0) - (a.state?.daysOverdue ?? 0))
    .map((r) => `${r.key} ${r.name} — ${r.assigneeName ?? t('ind.unassigned')} — ${fmtDate(r.dueDate)} (${t('ind.overdueD', { n: r.state?.daysOverdue ?? 0 })})`))
  return lines.join('\n')
}
