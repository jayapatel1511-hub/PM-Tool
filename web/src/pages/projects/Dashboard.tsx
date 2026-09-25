import { useQuery } from '@tanstack/react-query'
import { CalendarCheck, Plus } from 'lucide-react'
import { useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { type ActivityRow, ChangeList } from '@/components/hub/activity'
import { AttentionPanel } from '@/components/hub/attention'
import { Empty, ErrorBanner, Loading, Page, Section } from '@/components/hub/common'
import { useItemPanel } from '@/components/hub/panel-host'
import { HealthPill, Key, Pill, StatusPill, toneOf } from '@/components/hub/pills'
import { Why } from '@/components/hub/why'
import { Button } from '@/components/ui/button'
import { get, qs } from '@/lib/api'
import { fmtDate, fmtTime, relative } from '@/lib/format'
import { t } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { MilestoneStrip, type MilestoneRow } from './Milestones'
import { useCurrentProject } from './ProjectLayout'
import { CreateTask, TaskIndicators, useProjectLists, type TaskRow } from './Tasks'

interface Count { value: number; link: string }
interface Dash {
  milestones: (Omit<MilestoneRow, 'isComplete' | 'isCancelled'> & { reasons?: unknown })[]
  tasks: Record<'total' | 'complete' | 'inProgress' | 'review' | 'overdue' | 'blocked' | 'waiting' | 'unassigned', Count>
  deliverables: Record<'upcoming' | 'atRisk' | 'issued' | 'total', Count>
  decisions: Record<'pending' | 'overdue', Count>
  issues: { open: number; high: number }; risks: { high: number }
  disciplines: { disciplineId: string; name: string; leadId?: string; health: string; reasons: { text: string }[]; open: number; overdue: number; blocked: number; waiting: number
    deliverablesDue14: number; nextDueKey?: string; nextDueName?: string; nextDueDate?: string; nextDueType?: string; nextDueId?: string }[]
  dueThisWeek: { items: TaskRow[]; total: number; link: string }
  blocked: { items: TaskRow[]; total: number; link: string }
  activity: ActivityRow[]
}

/** Project Dashboard (§13.1): state and what needs attention, each number opening the list behind it. */
export function DashboardTab() {
  const p = useCurrentProject()
  const [sp, setSp] = useSearchParams()
  const [important, setImportant] = useState(false)
  const [creating, setCreating] = useState(false)
  const openPanel = useItemPanel()
  const lists = useProjectLists(p.id)
  const disciplineId = sp.get('discipline') ?? undefined
  const q = useQuery({ queryKey: ['p', p.id, 'dashboard', disciplineId, important], queryFn: () => get<Dash>(`projects/${p.id}/dashboard${qs({ disciplineId, importantOnly: important })}`) })
  const base = `/projects/${p.projectNumber}`
  const lead = (id?: string) => p.disciplines.find((x) => x.leadUserId === id)?.leadName
  const canSnooze = p.permissions.isPm || p.permissions.leadOf.length > 0
  return (
    <Page title={t('ptab.dashboard')} actions={<>
      <select className="h-8 rounded-md border bg-card px-2 text-sm" value={disciplineId ?? ''} aria-label={t('dash.scope')}
        onChange={(e) => { const n = new URLSearchParams(sp); if (e.target.value) n.set('discipline', e.target.value); else n.delete('discipline'); setSp(n, { replace: true }) }}>
        <option value="">{t('dash.allDisciplines')}</option>{p.disciplines.filter((d) => d.isActive).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
      </select>
      <Button asChild variant="outline" size="sm"><Link to={`${base}/coordination`}><CalendarCheck className="size-4" />{t('ptab.coordination')}</Link></Button>
      {p.permissions.createTaskIn.length > 0 && <Button size="sm" onClick={() => setCreating(true)}><Plus className="size-4" />{t('task.new')}</Button>}
    </>}>
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      {!q.data ? (q.isPending ? <Loading rows={8} /> : null) : (
        <>
          {q.data.milestones.length > 0 && <MilestoneStrip rows={q.data.milestones.map((m) => ({ ...m, isComplete: false, isCancelled: false }) as MilestoneRow)} onOpen={(m) => openPanel('Milestone', m.id)} />}
          <Tiles />
          <Counts d={q.data} base={base} />
          <div className="grid gap-4 xl:grid-cols-[3fr_2fr]">
            <Section title={t('dash.attention')} id="attention">
              <AttentionPanel projectId={p.id} canSnooze={canSnooze} limit={10} disciplineId={disciplineId} />
            </Section>
            <Section title={t('dash.disciplines')} id="disciplines">
              {q.data.disciplines.length === 0 ? <Empty>{t('dash.notEvaluated')}</Empty> : (
                <div className="overflow-x-auto">
                  <table className="w-full text-sm">
                    <thead className="bg-muted/60 text-left text-xs text-muted-foreground">
                      <tr>{['common.discipline', 'dash.lead', 'common.status', 'dash.open', 'ind.overdue', 'ind.blocked', 'dash.due14', 'dash.nextDue'].map((h) => <th key={h} className="whitespace-nowrap px-3 py-1.5 font-medium">{t(h)}</th>)}</tr>
                    </thead>
                    <tbody>
                      {q.data.disciplines.map((d) => (
                        <tr key={d.disciplineId} className="border-t">
                          <td className="px-3 py-1.5"><Link className="hover:underline" to={`${base}/tasks?disciplineId=${d.disciplineId}`}>{d.name}</Link></td>
                          <td className="whitespace-nowrap px-3 py-1.5">{lead(d.leadId) ?? <span className="text-warn">{t('dash.noLead')}</span>}</td>
                          <td className="px-3 py-1.5"><Why reasons={d.reasons} title={d.name}><HealthPill health={d.health} /></Why></td>
                          <td className="px-3 py-1.5 tabular-nums"><Link className="hover:underline" to={`${base}/tasks?disciplineId=${d.disciplineId}&open=true`}>{d.open}</Link></td>
                          <td className={cn('px-3 py-1.5 tabular-nums', d.overdue > 0 && 'font-medium text-bad')}><Link className="hover:underline" to={`${base}/tasks?disciplineId=${d.disciplineId}&overdue=true`}>{d.overdue}</Link></td>
                          <td className={cn('px-3 py-1.5 tabular-nums', d.blocked > 0 && 'font-medium text-bad')}><Link className="hover:underline" to={`${base}/tasks?disciplineId=${d.disciplineId}&blocked=true`}>{d.blocked}</Link></td>
                          <td className="px-3 py-1.5 tabular-nums">{d.deliverablesDue14}</td>
                          <td className="max-w-48 truncate px-3 py-1.5 text-xs" title={d.nextDueName}>{d.nextDueKey ? <><Key>{d.nextDueKey}</Key> {fmtDate(d.nextDueDate)}</> : t('common.dash')}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </Section>
          </div>
          <div className="grid gap-4 md:grid-cols-2">
            <TaskListCard title={t('dash.dueThisWeek')} data={q.data.dueThisWeek} to={`${base}/tasks?${q.data.dueThisWeek.link}`} />
            <TaskListCard title={t('ind.blocked')} data={q.data.blocked} to={`${base}/tasks?${q.data.blocked.link}`} />
          </div>
          <Section title={t('dash.activity')} id="activity" actions={
            <label className="flex items-center gap-1.5 text-xs"><input type="checkbox" checked={important} onChange={(e) => setImportant(e.target.checked)} />{t('activity.important')}</label>}>
            {q.data.activity.length === 0 ? <Empty>{t('activity.empty')}</Empty> : (
              <ol className="divide-y">
                {q.data.activity.map((r) => (
                  <li key={r.id} className="px-4 py-2 text-sm">
                    <div className="flex flex-wrap items-baseline gap-x-2">
                      <span className="font-medium">{r.actorName ?? t('common.system')}</span>
                      <span className="text-muted-foreground">{t(`action.${r.action}`)}</span>
                      {r.itemKey && <Key>{r.itemKey}</Key>}<span>{r.itemName}</span>
                      <time className="ml-auto text-xs text-muted-foreground" dateTime={r.occurredAt}>{fmtTime(r.occurredAt)}</time>
                    </div>
                    <div className="text-[13px]"><ChangeList row={r} /></div>
                  </li>
                ))}
              </ol>
            )}
            <div className="border-t px-4 py-2 text-xs"><Link className="text-primary hover:underline" to={`${base}/activity`}>{t('dash.allActivity')}</Link></div>
          </Section>
        </>
      )}
      {creating && <CreateTask p={p} deliverables={lists.deliverables} milestones={lists.milestones} defaults={{ projectDisciplineId: disciplineId }}
        onClose={(id) => { setCreating(false); if (id) { q.refetch(); openPanel('Task', id) } }} />}
    </Page>
  )
}

/** Health, next milestone, next submission and phase (§13.1 item 3). */
function Tiles() {
  const p = useCurrentProject()
  const h = p.health
  const tile = 'rounded-lg border bg-card px-4 py-3'
  return (
    <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
      <div className={tile}>
        <div className="text-xs text-muted-foreground">{t('dash.health')}</div>
        <div className="mt-1 flex flex-wrap items-center gap-1.5">
          <Why reasons={h.reasons} title={t('health.whyTitle', { health: t(`health.${h.computed}`) })}><HealthPill health={h.computed} label={h.overrideActive ? t('health.computed') : undefined} /></Why>
          {h.overrideActive && <HealthPill health={h.reported} label={t('health.reported')} />}
        </div>
        {h.overrideActive && h.healthOverrideNote && <p className="mt-1 line-clamp-2 text-xs text-muted-foreground">{h.healthOverrideNote}</p>}
      </div>
      {[['dash.nextMilestone', p.nextMilestone], ['dash.nextSubmission', p.nextSubmission]].map(([label, m]: any) => (
        <div key={label} className={tile}>
          <div className="text-xs text-muted-foreground">{t(label)}</div>
          {m ? <div className="mt-1 text-sm"><div className="truncate font-medium">{m.name}</div><div className="text-muted-foreground">{fmtDate(m.date)} · {relative(m.date)}</div></div>
            : <div className="mt-1 text-sm text-muted-foreground">{t('common.none')}</div>}
        </div>
      ))}
      <div className={tile}>
        <div className="text-xs text-muted-foreground">{t('projects.col.phase')}</div>
        <div className="mt-1 text-sm font-medium">{p.phase ?? t('common.dash')}</div>
        <div className="mt-1"><StatusPill status={p.status} /></div>
      </div>
    </div>
  )
}

function Counts({ d, base }: { d: Dash; base: string }) {
  const tone = (key: string, n: number) => n > 0 && ['overdue', 'blocked', 'atRisk', 'issuesHigh', 'risksHigh'].includes(key) ? 'text-bad' : n > 0 && ['waiting', 'unassigned'].includes(key) ? 'text-warn' : ''
  const group = (title: string, entries: [string, Count, string][]) => (
    <div className="rounded-lg border bg-card px-4 py-3">
      <div className="mb-1.5 text-xs font-semibold text-muted-foreground">{title}</div>
      <dl className="flex flex-wrap gap-x-5 gap-y-2">
        {entries.map(([key, c, to]) => (
          <div key={key} className="min-w-16">
            <dt className="text-xs text-muted-foreground">{t(`dash.count.${key}`)}</dt>
            <dd><Link to={to} className={cn('text-xl font-semibold tabular-nums hover:underline', tone(key, c.value))}>{c.value}</Link></dd>
          </div>
        ))}
      </dl>
    </div>
  )
  const tasks = (k: keyof Dash['tasks']) => [k, d.tasks[k], `${base}/tasks?${d.tasks[k].link}`] as [string, Count, string]
  const dels = (k: keyof Dash['deliverables']) => [k, d.deliverables[k], `${base}/deliverables?${d.deliverables[k].link}`] as [string, Count, string]
  const decs = (k: keyof Dash['decisions']) => [`dec${k}`, d.decisions[k], `${base}/decisions?${d.decisions[k].link}`] as [string, Count, string]
  return (
    <div className="grid gap-3 lg:grid-cols-[2fr_1fr_auto]">
      {group(t('ptab.tasks'), [tasks('total'), tasks('complete'), tasks('inProgress'), tasks('review'), tasks('overdue'), tasks('blocked'), tasks('waiting'), tasks('unassigned')])}
      {group(t('ptab.deliverables'), [dels('upcoming'), dels('atRisk'), dels('issued'), dels('total')])}
      {group(t('ptab.decisions'), [decs('pending'), decs('overdue')])}
      {group(t('dash.registers'), [
        ['issuesOpen', { value: d.issues.open, link: '' }, `${base}/issues?indicator=open`],
        ['issuesHigh', { value: d.issues.high, link: '' }, `${base}/issues?indicator=open&severity=High`],
        ['risksHigh', { value: d.risks.high, link: '' }, `${base}/risks?indicator=open&severity=High`],
      ])}
    </div>
  )
}

function TaskListCard({ title, data, to }: { title: string; data: { items: TaskRow[]; total: number }; to: string }) {
  const openPanel = useItemPanel()
  return (
    <Section title={title} count={data.total} actions={data.total > data.items.length && <Link to={to} className="text-xs text-primary hover:underline">{t('attention.showAll', { n: data.total })}</Link>}>
      {data.items.length === 0 ? <Empty>{t('common.none')}</Empty> : (
        <ul className="divide-y">
          {data.items.map((r) => (
            <li key={r.id} className="flex flex-wrap items-center gap-2 px-4 py-2 text-sm">
              <Key>{r.key}</Key>
              <button className="min-w-0 flex-1 truncate text-left hover:underline" onClick={() => openPanel('Task', r.id)}>{r.name}</button>
              <span className="text-xs text-muted-foreground">{r.assigneeName ?? t('ind.unassigned')}</span>
              <Pill tone={toneOf(r.state?.isOverdue ? 'Overdue' : r.status)} icon={false}>{fmtDate(r.dueDate)}</Pill>
              <TaskIndicators r={r} />
            </li>
          ))}
        </ul>
      )}
    </Section>
  )
}
