import { useQuery, useQueryClient } from '@tanstack/react-query'
import { CheckCheck, ChevronDown, ChevronRight, ClipboardCopy, ListPlus, MessageSquare, MonitorPlay, Plus, Printer } from 'lucide-react'
import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { AttentionPanel } from '@/components/hub/attention'
import { ErrorBanner, Loading } from '@/components/hub/common'
import { InlineDate } from '@/components/hub/fields'
import { useItemPanel } from '@/components/hub/panel-host'
import { PeoplePicker } from '@/components/hub/people'
import { HealthPill, Key, PriorityBadge, StatusPill } from '@/components/hub/pills'
import { Button } from '@/components/ui/button'
import { get, post, qs } from '@/lib/api'
import { fmtDate, fmtTime, relative, today } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { RecordDecisionDialog } from './Decisions'
import type { DeliverableRow } from './Deliverables'
import { ActionForm, ActionOwner, type ActionRow } from './Meetings'
import { useCurrentProject } from './ProjectLayout'
import { CreateTask, StatusMenu, TaskIndicators, useProjectLists, useTaskActions, useTaskHints, type Blocker, type TaskRow } from './Tasks'
import { DisciplineCoordinationView } from './DisciplineCoordinationView'

interface Milestone { id: string; key: string; name: string; date: string; daysRemaining: number; status?: string; deliverableTotal: number; deliverableIssued: number; taskTotal: number; taskComplete: number }
interface Decision { id: string; key: string; subject: string; status: string; requiredByDate: string; impactLevel: string; ownerName?: string; isOverdue: boolean; daysOverdue: number; blocking: number }
interface Discipline { discipline: { disciplineId: string; name: string; leadId?: string; health: string; open: number; overdue: number; blocked: number; deliverablesDue14: number }; top: TaskRow[] }
interface Count { value: number; link: string }
interface Coord {
  window: { thisWeek: { from: string; to: string }; nextWeek: { from: string; to: string }; since: string; lastCoordinationReviewedAt?: string; reviewedBy?: string; canMarkReviewed: boolean }
  headline: { computedHealth?: string; overdueTasks: Count; blockedTasks: Count; decisionsOverdue: number; deliverablesAtRisk: Count; nextMilestone?: Milestone }
  milestones: Milestone[]; deliverables: DeliverableRow[]; decisions: Decision[]; blocked: TaskRow[]; overdue: TaskRow[]; dueThisWeek: TaskRow[]; disciplines: Discipline[]
  issues: { id: string; key: string; title: string; status: string; severity: string; targetResolutionDate?: string }[]
  risks: { id: string; key: string; title: string; status: string; probability: number; impact: number }[]
  waiting: ActionRow[]
  completed: { tasks: TaskRow[]; deliverables: DeliverableRow[]; decisions: { id: string; key: string; subject: string; decisionDate?: string }[] }
  upcoming: { tasks: TaskRow[]; deliverables: DeliverableRow[] }
  held: { tasks: TaskRow[]; deliverables: DeliverableRow[] }
}

/** Blocked work grouped by what blocks it, so the meeting talks about each cause once (§12.13 section 6, AC-WC-02). */
function byBlocker(rows: TaskRow[]) {
  const groups = new Map<string, { label: string; blocker: Blocker; rows: TaskRow[] }>()
  for (const r of rows)
    for (const b of (r.state?.blockedBy ?? []).filter((x) => x.blocking)) {
      const id = b.type === 'manual' ? `manual:${b.name}:${b.reason}` : `${b.type}:${b.id}`
      const label = b.type === 'manual' ? t('task.manualBlocker', { type: tv(b.name ?? ''), reason: b.reason ?? '' }) : `${b.key} ${b.name}`
      const g = groups.get(id) ?? { label, blocker: b, rows: [] }
      g.rows.push(r)
      groups.set(id, g)
    }
  return [...groups.values()].sort((a, b) => Math.max(...b.rows.map((r) => r.state?.daysBlocked ?? 0)) - Math.max(...a.rows.map((r) => r.state?.daysBlocked ?? 0)))
}

function byDiscipline<T extends { disciplineName?: string; disciplineOrder?: number }>(rows: T[]) {
  const m = new Map<string, T[]>()
  for (const r of [...rows].sort((a, b) => (a.disciplineOrder ?? 0) - (b.disciplineOrder ?? 0))) m.set(r.disciplineName ?? '', [...(m.get(r.disciplineName ?? '') ?? []), r])
  return [...m.entries()]
}

/** Weekly Coordination (§12.13, §13.9): the meeting agenda computed from live data, with meeting mode. */
export function CoordinationTab() {
  const p = useCurrentProject()
  const [sp, setSp] = useSearchParams()
  const qc = useQueryClient()
  const openPanel = useItemPanel()
  const hints = useTaskHints(p)
  const lists = useProjectLists(p.id)
  const disciplineId = sp.get('discipline') ?? undefined
  const meeting = sp.get('meeting') === '1'
  const set = (k: string, v?: string) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); setSp(n, { replace: true }) }
  const q = useQuery({ queryKey: ['p', p.id, 'coordination', disciplineId], queryFn: () => get<Coord>(`projects/${p.id}/coordination${qs({ disciplineId })}`) })
  const [log, setLog] = useState<{ id: string; key: string; what: string; at: string }[]>([])
  const [hideDiscussed, setHideDiscussed] = useState(false)
  const [collapsed, setCollapsed] = useState<Set<string>>(new Set())
  const [creating, setCreating] = useState(false)
  const [recording, setRecording] = useState<{ id: string; key: string } | null>(null)
  const [capturing, setCapturing] = useState<{ taskId?: string; decisionId?: string; label?: string; ownerId?: string; ownerName?: string;
    links?: { targetType: string; targetId: string }[] } | null>(null)
  const reload = () => { q.refetch(); qc.invalidateQueries({ queryKey: ['p', p.id] }) }
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

  if (q.isPending) return <Loading rows={10} />
  if (q.error) return <div className="p-6"><ErrorBanner error={q.error} retry={() => q.refetch()} /></div>
  const c = q.data
  const visible = (rows: TaskRow[]) => (hideDiscussed ? rows.filter((r) => !touched.has(r.id)) : rows)

  const canCapture = c.window.canMarkReviewed // MTG-04: the PM and leads capture actions in the meeting
  const markReviewed = async () => { await post(`projects/${p.id}/coordination/reviewed`); toast.success(t('wc.reviewedToast')); reload() }
  const copySummary = async () => {
    await navigator.clipboard.writeText(summaryText(p.projectNumber, p.name, c))
    toast.success(t('wc.copied'))
  }

  const row = (r: TaskRow, extra?: ReactNode) => (
    <li key={r.id} className={cn('flex flex-wrap items-center gap-x-3 gap-y-1 px-4 py-2', touched.has(r.id) && 'bg-ok-bg/40')}>
      <button className="hover:underline" onClick={() => openPanel('Task', r.id)}><Key>{r.key}</Key></button>
      <button className="min-w-[10rem] flex-1 text-left font-medium hover:underline" onClick={() => openPanel('Task', r.id)}>{r.name}</button>
      {extra}
      {meeting && hints.manages(r) ? (
        <span className="w-44"><PeoplePicker compact value={r.assigneeId} valueName={r.assigneeName} placeholder={t('ind.unassigned')} label={t('task.assigneeOf', { key: r.key })} onChange={(id) => actions.save(r, { assigneeId: id })} /></span>
      ) : <span className="text-muted-foreground">{r.assigneeName ?? t('ind.unassigned')}</span>}
      {meeting && hints.due(r)
        ? <span className="w-36"><InlineDate value={r.dueDate} title={t('task.dueOf', { key: r.key })} onSave={(v) => actions.save(r, { dueDate: v })} /></span>
        : <span className={cn('tabular-nums', r.state?.isOverdue && 'font-medium text-bad')}>{fmtDate(r.dueDate)}</span>}
      <StatusMenu r={r} onMove={actions.move} disabled={!meeting && false} />
      <PriorityBadge priority={r.priority === 'High' || r.priority === 'Critical' ? r.priority : null} />
      <TaskIndicators r={r} />
      {meeting && <Button variant="ghost" size="icon" className="size-7" aria-label={t('wc.commentOn', { key: r.key })} onClick={() => openPanel('Task', r.id)}><MessageSquare className="size-3.5" /></Button>}
      {meeting && canCapture && <Button variant="ghost" size="icon" className="size-7" aria-label={t('wc.actionOn', { key: r.key })}
        onClick={() => setCapturing({ taskId: r.id, label: `${r.key} ${r.name}`, ownerId: r.assigneeId ?? undefined, ownerName: r.assigneeName ?? undefined })}><ListPlus className="size-3.5" /></Button>}
    </li>
  )
  const delRow = (d: DeliverableRow, extra?: ReactNode) => (
    <li key={d.id} className="flex flex-wrap items-center gap-x-3 gap-y-1 px-4 py-2">
      <button className="hover:underline" onClick={() => openPanel('Deliverable', d.id)}><Key>{d.key}</Key></button>
      <button className="min-w-0 flex-1 text-left font-medium hover:underline" onClick={() => openPanel('Deliverable', d.id)}>{d.name}</button>
      {extra}
      <span className="text-muted-foreground">{d.ownerName ?? t('ind.unassigned')}</span>
      <span className={cn('tabular-nums', d.state?.isOverdue && 'font-medium text-bad')}>{fmtDate(d.dueDate)}</span>
      <StatusPill status={d.status} />
    </li>
  )
  const list = (items: ReactNode[]) => items.length ? <ul className="divide-y">{items}</ul> : <p className="px-4 py-3 text-muted-foreground">{t('wc.nothing')}</p>
  const grouped = (groups: [string, ReactNode[]][]) => groups.length === 0 ? list([]) : groups.map(([g, items]) => (
    <div key={g}><div className="bg-muted/40 px-4 py-1 text-xs font-semibold text-muted-foreground">{g}</div><ul className="divide-y">{items}</ul></div>
  ))

  const blockedGroups = byBlocker(visible(c.blocked))
  const sections: { id: string; title: string; count: number; body: ReactNode }[] = [
    { id: 'headline', title: t('wc.s.headline'), count: 0, body: (
      <div className="flex flex-wrap items-center gap-x-6 gap-y-2 px-4 py-3">
        <HealthPill health={c.headline.computedHealth ?? p.health.computed} label={t('health.computed')} />
        {p.health.overrideActive && <HealthPill health={p.health.reported} label={t('health.reported')} />}
        {c.headline.nextMilestone && <span>◆ {c.headline.nextMilestone.name} · {fmtDate(c.headline.nextMilestone.date)} ({relative(c.headline.nextMilestone.date)})</span>}
        {p.nextSubmission && <span>{t('dash.nextSubmission')}: {p.nextSubmission.name} · {fmtDate(p.nextSubmission.date)}</span>}
        <span>{t('wc.overdueN', { n: c.headline.overdueTasks.value })}</span><span>{t('wc.blockedN', { n: c.headline.blockedTasks.value })}</span>
        <span>{t('wc.decisionsOverdueN', { n: c.headline.decisionsOverdue })}</span><span>{t('wc.atRiskN', { n: c.headline.deliverablesAtRisk.value })}</span>
      </div>) },
    { id: 'attention', title: t('wc.s.attention'), count: -1, body: <AttentionPanel projectId={p.id} canSnooze={c.window.canMarkReviewed} disciplineId={disciplineId} severity="Critical,Warning" /> },
    { id: 'milestones', title: t('wc.s.milestones'), count: c.milestones.length, body: list(c.milestones.map((m) => (
      <li key={m.id} className="flex flex-wrap items-center gap-x-3 px-4 py-2">
        <button className="hover:underline" onClick={() => openPanel('Milestone', m.id)}><Key>{m.key}</Key> <span className="font-medium">{m.name}</span></button>
        <span>{fmtDate(m.date)} ({relative(m.date)})</span><StatusPill status={m.status} />
        <span className="text-muted-foreground">{t('wc.prereqs', { issued: m.deliverableIssued, total: m.deliverableTotal, done: m.taskComplete, tasks: m.taskTotal })}</span>
      </li>))) },
    { id: 'deliverables', title: t('wc.s.deliverables'), count: c.deliverables.length, body: grouped(byDiscipline(c.deliverables).map(([g, rows]) => [g, rows.map((d) => delRow(d))])) },
    { id: 'decisions', title: t('wc.s.decisions'), count: c.decisions.length, body: list(c.decisions.map((d) => (
      <li key={d.id} className="flex flex-wrap items-center gap-x-3 px-4 py-2">
        <button className="hover:underline" onClick={() => openPanel('Decision', d.id)}><Key>{d.key}</Key> <span className="font-medium">{d.subject}</span></button>
        <span className="text-muted-foreground">{d.ownerName ?? t('ind.unassigned')}</span>
        <span className={cn(d.isOverdue && 'font-medium text-bad')}>{t('wc.requiredBy', { date: fmtDate(d.requiredByDate) })}{d.isOverdue && ` · ${t('ind.overdueD', { n: d.daysOverdue })}`}</span>
        {d.blocking > 0 && <span className="text-bad">{t('ind.blocking', { n: d.blocking })}</span>}<StatusPill status={d.status} />
        {meeting && <Button size="sm" variant="outline" className="no-print ml-auto h-7" onClick={() => setRecording({ id: d.id, key: d.key })}>{t('decision.record')}</Button>}
        {meeting && canCapture && <Button variant="ghost" size="icon" className="no-print size-7" aria-label={t('wc.actionOn', { key: d.key })}
          onClick={() => setCapturing({ decisionId: d.id, label: `${d.key} ${d.subject}` })}><ListPlus className="size-3.5" /></Button>}
      </li>))) },
    { id: 'blocked', title: t('wc.s.blocked'), count: c.blocked.length, body: blockedGroups.length === 0 ? list([]) : blockedGroups.map((g) => (
      <div key={g.label}>
        <div className="flex flex-wrap items-center gap-2 bg-bad-bg/60 px-4 py-1.5 text-sm font-semibold text-bad">
          {t('wc.blockedBy', { what: g.label })}{g.blocker.overdue && <span className="text-xs font-normal">({t('ind.overdue')})</span>}
          {g.blocker.type === 'task' && g.blocker.id && <button className="text-xs font-normal underline" onClick={() => openPanel('Task', g.blocker.id!)}>{t('wc.openCause')}</button>}
          <span className="ml-auto text-xs font-normal">{t('wc.tasksN', { n: g.rows.length })}</span>
        </div>
        <ul className="divide-y">{g.rows.map((r) => row(r, <span className="text-xs text-muted-foreground">{t('ind.blockedD', { n: r.state?.daysBlocked ?? 0 })}</span>))}</ul>
      </div>)) },
    { id: 'overdue', title: t('wc.s.overdue'), count: c.overdue.length, body: grouped(byDiscipline([...visible(c.overdue)].sort((a, b) => (b.state?.daysOverdue ?? 0) - (a.state?.daysOverdue ?? 0))).map(([g, rows]) => [g, rows.map((r) => row(r))])) },
    { id: 'thisWeek', title: t('wc.s.thisWeek'), count: c.dueThisWeek.length, body: grouped(byDiscipline(visible(c.dueThisWeek)).map(([g, rows]) => [g, rows.map((r) => row(r))])) },
    { id: 'round', title: t('wc.s.round'), count: c.disciplines.length, body: (
      <div className="grid gap-3 p-4 lg:grid-cols-2">
        {c.disciplines.map(({ discipline: d, top }) => (
          <div key={d.disciplineId} className="rounded-md border">
            <div className="flex flex-wrap items-center gap-2 border-b px-3 py-2">
              <span className="font-semibold">{d.name}</span><HealthPill health={d.health} />
              <span className="text-muted-foreground">{p.disciplines.find((x) => x.id === d.disciplineId)?.leadName ?? t('dash.noLead')}</span>
              <span className="ml-auto text-xs text-muted-foreground">{t('wc.roundCounts', { open: d.open, overdue: d.overdue, blocked: d.blocked, due: d.deliverablesDue14 })}</span>
            </div>
            <ul className="divide-y text-sm">{top.length === 0 ? <li className="px-3 py-2 text-muted-foreground">{t('wc.nothing')}</li> : top.map((r) => row(r))}</ul>
          </div>
        ))}
      </div>) },
    ...(c.issues.length + c.risks.length > 0 ? [{ id: 'issues', title: t('wc.s.issues'), count: c.issues.length + c.risks.length, body: list([
      ...c.issues.map((i) => <li key={i.id} className="flex gap-3 px-4 py-2"><button className="hover:underline" onClick={() => openPanel('Issue', i.id)}><Key>{i.key}</Key></button>
        <button className="flex-1 text-left hover:underline" onClick={() => openPanel('Issue', i.id)}>{i.title}</button><StatusPill status={i.severity === 'High' ? 'Critical' : i.status} /></li>),
      ...c.risks.map((r) => <li key={r.id} className="flex gap-3 px-4 py-2"><button className="hover:underline" onClick={() => openPanel('Risk', r.id)}><Key>{r.key}</Key></button>
        <button className="flex-1 text-left hover:underline" onClick={() => openPanel('Risk', r.id)}>{r.title}</button><span className="text-bad">{t('wc.riskScore', { n: r.probability * r.impact })}</span></li>),
    ]) }] : []),
    ...(c.waiting.length > 0 ? [{ id: 'waiting', title: t('wc.s.waiting'), count: c.waiting.length, body: list(c.waiting.map((a) => (
      <li key={a.id} className={cn('flex flex-wrap items-center gap-x-3 gap-y-1 px-4 py-2', a.isOverdue && 'bg-bad-bg/30')}>
        <button className="hover:underline" onClick={() => openPanel('Action', a.id)}><Key>{a.key}</Key></button>
        <button className="min-w-[10rem] flex-1 text-left font-medium hover:underline" onClick={() => openPanel('Action', a.id)}>{a.text}</button>
        <ActionOwner a={a} /><span className={cn('tabular-nums', a.isOverdue && 'font-medium text-bad')}>{fmtDate(a.dueDate)}{a.isOverdue && ` · ${t('ind.overdueD', { n: a.daysOverdue })}`}</span>
        <StatusPill status={a.status} />
      </li>))) }] : []),
    { id: 'completed', title: t('wc.s.completed'), count: c.completed.tasks.length + c.completed.deliverables.length + c.completed.decisions.length, body: list([
      ...c.completed.tasks.map((r) => row(r, <span className="text-xs text-ok">{t('wc.completedOn', { date: fmtDate(r.completedAt) })}</span>)),
      ...c.completed.deliverables.map((d) => delRow(d, <span className="text-xs text-ok">{t('wc.issuedOn', { date: fmtDate(d.issuedDate) })}</span>)),
      ...c.completed.decisions.map((d) => <li key={d.id} className="flex gap-3 px-4 py-2"><Key>{d.key}</Key><span className="flex-1">{d.subject}</span><span className="text-xs text-ok">{t('wc.decidedOn', { date: fmtDate(d.decisionDate) })}</span></li>),
    ]) },
    { id: 'upcoming', title: t('wc.s.upcoming'), count: c.upcoming.tasks.length + c.upcoming.deliverables.length, body: list([...c.upcoming.tasks.map((r) => row(r)), ...c.upcoming.deliverables.map((d) => delRow(d))]) },
    { id: 'held', title: t('wc.s.held'), count: c.held.tasks.length + c.held.deliverables.length, body: list([
      ...c.held.tasks.map((r) => row(r, <span className="text-xs text-muted-foreground">{t('wc.heldDays', { n: daysSince(r.statusChangedAt) })}</span>)),
      ...c.held.deliverables.map((d) => delRow(d)),
    ]) },
  ]

  return (
    <div ref={root} className={cn('mx-auto flex w-full flex-col gap-4 p-4 lg:p-6', meeting && 'text-[17px] [&_.text-xs]:text-sm [&_.text-\\[13px\\]]:text-base')}>
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">{t('ptab.coordination')}</h1>
          <p className="text-sm text-muted-foreground">
            {t('wc.window', { from: fmtDate(c.window.thisWeek.from), to: fmtDate(c.window.thisWeek.to) })} ·{' '}
            {c.window.lastCoordinationReviewedAt ? t('wc.sinceReview', { when: fmtTime(c.window.lastCoordinationReviewedAt), who: c.window.reviewedBy ?? '' }) : t('wc.neverReviewed')}
          </p>
        </div>
        <div className="no-print flex flex-wrap items-center gap-2">
          {!meeting && (
            <select className="h-8 rounded-md border bg-card px-2 text-sm" value={disciplineId ?? ''} onChange={(e) => set('discipline', e.target.value || undefined)} aria-label={t('dash.scope')}>
              <option value="">{t('dash.allDisciplines')}</option>{p.disciplines.filter((d) => d.isActive).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
          )}
          {meeting && <label className="flex items-center gap-1.5 text-sm"><input type="checkbox" checked={hideDiscussed} onChange={(e) => setHideDiscussed(e.target.checked)} />{t('wc.hideDiscussed')}</label>}
          {meeting && p.permissions.createTaskIn.length > 0 && <Button size="sm" variant="outline" onClick={() => setCreating(true)}><Plus className="size-4" />{t('task.new')}</Button>}
          {meeting && canCapture && <Button size="sm" variant="outline" onClick={() => setCapturing({})}><ListPlus className="size-4" />{t('action.new')}</Button>}
          <Button size="sm" variant={meeting ? 'default' : 'outline'} aria-pressed={meeting} onClick={() => set('meeting', meeting ? undefined : '1')}><MonitorPlay className="size-4" />{t('wc.meetingMode')}</Button>
          <Button size="sm" variant="outline" onClick={copySummary}><ClipboardCopy className="size-4" />{t('wc.copySummary')}</Button>
          <Button size="sm" variant="outline" onClick={() => window.print()}><Printer className="size-4" />{t('wc.print')}</Button>
          {c.window.canMarkReviewed && <Button size="sm" onClick={markReviewed}><CheckCheck className="size-4" />{t('wc.markReviewed')}</Button>}
        </div>
      </div>
      {p.status !== 'Active' && <div role="status" className="rounded-md border bg-idle-bg px-3 py-2 text-sm text-idle">{t('wc.notActive', { status: tv(p.status) })}</div>}
      <DisciplineCoordinationView project={p} disciplineId={disciplineId} meeting={meeting} canCapture={canCapture}
        onCapture={(label, links) => setCapturing({ label, links })} />
      <div className="grid gap-4 lg:grid-cols-[12rem_1fr]">
        <nav aria-label={t('wc.index')} className="no-print hidden lg:block">
          <ol className="sticky top-2 space-y-0.5 text-sm">
            {sections.map((s, i) => (
              <li key={s.id}><a href={`#wc-${s.id}`} className="flex justify-between rounded px-2 py-1 hover:bg-muted"><span>{i + 1}. {s.title}</span>{s.count > 0 && <span className="text-muted-foreground">{s.count}</span>}</a></li>
            ))}
          </ol>
        </nav>
        <div className="min-w-0 space-y-3">
          {sections.map((s, i) => {
            const shut = collapsed.has(s.id)
            return (
              <section key={s.id} id={`wc-${s.id}`} className="scroll-mt-2 rounded-lg border bg-card" aria-labelledby={`wc-h-${s.id}`}>
                <h2 id={`wc-h-${s.id}`} data-section-head tabIndex={-1} className="flex items-center gap-2 border-b px-4 py-2.5 font-semibold outline-none focus-visible:ring-2 focus-visible:ring-primary">
                  <button className="rounded p-0.5 hover:bg-muted" aria-expanded={!shut} aria-label={shut ? t('wc.expand') : t('wc.collapse')}
                    onClick={() => setCollapsed((x) => { const n = new Set(x); if (n.has(s.id)) n.delete(s.id); else n.add(s.id); return n })}>
                    {shut ? <ChevronRight className="size-4" /> : <ChevronDown className="size-4" />}
                  </button>
                  {i + 1}. {s.title}{s.count > 0 && <span className="rounded-full bg-muted px-2 text-xs font-medium text-muted-foreground">{s.count}</span>}
                </h2>
                {!shut && <div className="text-sm">{s.body}</div>}
              </section>
            )
          })}
        </div>
      </div>
      {meeting && log.length > 0 && (
        <aside aria-label={t('wc.tray')} className="no-print fixed bottom-16 right-4 z-30 max-h-64 w-80 overflow-y-auto rounded-lg border bg-card p-3 text-sm shadow-lg md:bottom-4">
          <div className="mb-1 font-semibold">{t('wc.tray')} ({log.length})</div>
          <ul className="space-y-1">{log.map((x, i) => <li key={i}><Key>{x.key}</Key> {x.what}</li>)}</ul>
        </aside>
      )}
      {recording && <RecordDecisionDialog id={recording.id} onClose={(ok) => { if (ok) { setLog((l) => [...l, { id: recording.id, key: recording.key, what: t('decision.recordedShort'), at: new Date().toISOString() }]); reload() } setRecording(null) }} />}
      {creating && <CreateTask p={p} deliverables={lists.deliverables} milestones={lists.milestones} defaults={{ projectDisciplineId: disciplineId }}
        onClose={(id) => { setCreating(false); if (id) { setLog((l) => [...l, { id, key: t('wc.newTask'), what: t('wc.created'), at: new Date().toISOString() }]); reload() } }} />}
      {capturing && <ActionForm projectId={p.id} related={capturing.label ? { taskId: capturing.taskId, decisionId: capturing.decisionId, label: capturing.label } : undefined}
        links={capturing.links}
        defaultOwner={capturing.ownerId ? { type: 'User', userId: capturing.ownerId, userName: capturing.ownerName } : undefined}
        onClose={(created) => { setCapturing(null); if (created) { setLog((l) => [...l, { id: created.id, key: created.key, what: t('wc.actionAdded', { text: created.text }), at: new Date().toISOString() }]); reload() } }} />}
      {actions.dialogs}
    </div>
  )
}

function daysSince(ts?: string) {
  if (!ts) return 0
  return Math.max(0, Math.round((Date.parse(today() + 'T00:00:00Z') - Date.parse(ts.slice(0, 10) + 'T00:00:00Z')) / 86_400_000))
}

/** Plain text of sections 1, 3, 5, 6 and 7 with item keys, for the meeting notes (FR-005, AC-WC-06). */
export function summaryText(number: string, name: string, c: Coord) {
  const lines = [`${number} ${name} — ${t('ptab.coordination')} ${fmtDate(today())}`, '']
  const h = c.headline
  lines.push(`${t('wc.s.headline')}: ${t(`health.${h.computedHealth ?? 'Grey'}`)} · ${t('wc.overdueN', { n: h.overdueTasks.value })} · ${t('wc.blockedN', { n: h.blockedTasks.value })} · ${t('wc.decisionsOverdueN', { n: h.decisionsOverdue })} · ${t('wc.atRiskN', { n: h.deliverablesAtRisk.value })}`)
  if (h.nextMilestone) lines.push(`${t('dash.nextMilestone')}: ${h.nextMilestone.key} ${h.nextMilestone.name} ${fmtDate(h.nextMilestone.date)} (${relative(h.nextMilestone.date)})`)
  const block = (title: string, rows: string[]) => { lines.push('', title); lines.push(...(rows.length ? rows.map((r) => `- ${r}`) : [`- ${t('wc.nothing')}`])) }
  block(t('wc.s.milestones'), c.milestones.map((m) => `${m.key} ${m.name} — ${fmtDate(m.date)} (${relative(m.date)}) — ${tv(m.status ?? '')} — ${t('wc.prereqs', { issued: m.deliverableIssued, total: m.deliverableTotal, done: m.taskComplete, tasks: m.taskTotal })}`))
  block(t('wc.s.decisions'), c.decisions.map((d) => `${d.key} ${d.subject} — ${t('wc.requiredBy', { date: fmtDate(d.requiredByDate) })}${d.isOverdue ? ` (${t('ind.overdueD', { n: d.daysOverdue })})` : ''}${d.ownerName ? ` — ${d.ownerName}` : ''}`))
  block(t('wc.s.blocked'), byBlocker(c.blocked).map((g) => `${t('wc.blockedBy', { what: g.label })}: ${g.rows.map((r) => `${r.key} ${r.name}`).join('; ')}`))
  block(t('wc.s.overdue'), [...c.overdue].sort((a, b) => (b.state?.daysOverdue ?? 0) - (a.state?.daysOverdue ?? 0))
    .map((r) => `${r.key} ${r.name} — ${r.assigneeName ?? t('ind.unassigned')} — ${fmtDate(r.dueDate)} (${t('ind.overdueD', { n: r.state?.daysOverdue ?? 0 })})`))
  return lines.join('\n')
}
