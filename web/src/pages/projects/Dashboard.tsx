import { useQuery } from '@tanstack/react-query'
import { CalendarCheck, Plus } from 'lucide-react'
import { useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { type ActivityRow, ChangeList } from '@/components/hub/activity'
import { AttentionPanel } from '@/components/hub/attention'
import { Empty, ErrorBanner, Loading, Missing, Page, Section, SummaryTile, selectCls, tdCls, thCls } from '@/components/hub/common'
import { useItemPanel } from '@/components/hub/panel-host'
import { Avatar } from '@/components/hub/people'
import { Chip, HealthPill, Key, StatusPill } from '@/components/hub/pills'
import { Why } from '@/components/hub/why'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { get, qs } from '@/lib/api'
import { fmtDate, fmtTime, relative } from '@/lib/format'
import { t } from '@/lib/i18n'
import { cn, type Accent } from '@/lib/utils'
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

// Section accents identify the section and repeat wherever it appears (design §3): tasks blue, disciplines mint, attention peach.
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
      <div className="flex items-center gap-2">
        <label htmlFor="dash-scope" className="text-sm font-medium">{t('dash.scope')}</label>
        <select id="dash-scope" className={cn(selectCls, 'w-auto max-w-56')} value={disciplineId ?? ''}
          onChange={(e) => { const n = new URLSearchParams(sp); if (e.target.value) n.set('discipline', e.target.value); else n.delete('discipline'); setSp(n, { replace: true }) }}>
          <option value="">{t('dash.allDisciplines')}</option>{p.disciplines.filter((d) => d.isActive).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
        </select>
      </div>
      <Button asChild variant="outline"><Link to={`${base}/coordination`}><CalendarCheck className="size-4" />{t('ptab.coordination')}</Link></Button>
      {p.permissions.createTaskIn.length > 0 && <Button onClick={() => setCreating(true)}><Plus className="size-4" />{t('task.new')}</Button>}
    </>}>
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      {!q.data ? (q.isPending ? <Loading rows={8} /> : null) : (
        <>
          {q.data.milestones.length > 0 && <MilestoneStrip rows={q.data.milestones.map((m) => ({ ...m, isComplete: false, isCancelled: false }) as MilestoneRow)} onOpen={(m) => openPanel('Milestone', m.id)} />}
          <Headline />
          <Counts d={q.data} base={base} />
          <div className="grid gap-6 xl:grid-cols-[3fr_2fr]">
            <Section title={t('dash.attention')} id="attention" accent="peach">
              <AttentionPanel projectId={p.id} canSnooze={canSnooze} limit={10} disciplineId={disciplineId} />
            </Section>
            <Section title={t('dash.disciplines')} id="disciplines" accent="mint">
              {q.data.disciplines.length === 0 ? <Empty>{t('dash.notEvaluated')}</Empty> : (
                <div className="scroll-region overflow-x-auto">
                  <table className="w-full text-sm">
                    <thead className="bg-muted">
                      <tr>{([['common.discipline'], ['dash.lead'], ['common.status'], ['dash.open', true], ['ind.overdue', true], ['ind.blocked', true], ['dash.due14', true], ['dash.nextDue']] as const)
                        .map(([h, num]) => <th key={h} scope="col" className={cn(thCls, num && 'text-right')}>{t(h)}</th>)}</tr>
                    </thead>
                    <tbody>
                      {q.data.disciplines.map((d) => {
                        const name = lead(d.leadId)
                        const tasks = `${base}/tasks?disciplineId=${d.disciplineId}`
                        return (
                          <tr key={d.disciplineId} className="border-t hover:bg-muted">
                            <td className={tdCls}><Link className="font-semibold hover:underline" to={tasks}>{d.name}</Link></td>
                            <td className={cn(tdCls, 'whitespace-nowrap')}>
                              {name ? <span className="inline-flex items-center gap-2"><Avatar id={d.leadId} name={name} />{name}</span> : <Chip tone="warn">{t('dash.noLead')}</Chip>}
                            </td>
                            <td className={tdCls}><Why reasons={d.reasons} title={d.name}><HealthPill health={d.health} /></Why></td>
                            <td className={cn(tdCls, 'text-right tabular-nums')}><Link className="hover:underline" to={`${tasks}&open=true`}>{d.open}</Link></td>
                            <td className={cn(tdCls, 'text-right tabular-nums', d.overdue > 0 && 'font-semibold text-bad')}><Link className="hover:underline" to={`${tasks}&overdue=true`}>{d.overdue}</Link></td>
                            <td className={cn(tdCls, 'text-right tabular-nums', d.blocked > 0 && 'font-semibold text-bad')}><Link className="hover:underline" to={`${tasks}&blocked=true`}>{d.blocked}</Link></td>
                            <td className={cn(tdCls, 'text-right tabular-nums')}>{d.deliverablesDue14}</td>
                            <td className={cn(tdCls, 'whitespace-nowrap')} title={d.nextDueName}>{d.nextDueKey ? <><Key>{d.nextDueKey}</Key> <span className="tabular-nums">{fmtDate(d.nextDueDate)}</span></> : <Missing />}</td>
                          </tr>
                        )
                      })}
                    </tbody>
                  </table>
                </div>
              )}
            </Section>
          </div>
          <div className="grid gap-6 md:grid-cols-2">
            <TaskListCard title={t('dash.dueThisWeek')} empty={t('dash.dueThisWeekEmpty')} data={q.data.dueThisWeek} to={`${base}/tasks?${q.data.dueThisWeek.link}`} />
            <TaskListCard title={t('ind.blocked')} empty={t('dash.blockedEmpty')} data={q.data.blocked} to={`${base}/tasks?${q.data.blocked.link}`} />
          </div>
          <Section title={t('dash.activity')} id="activity" actions={
            <label className="flex min-h-6 items-center gap-2 text-sm"><Checkbox checked={important} onCheckedChange={(c) => setImportant(c === true)} />{t('activity.important')}</label>}>
            {q.data.activity.length === 0 ? <Empty>{t('activity.empty')}</Empty> : (
              <ol className="divide-y">
                {q.data.activity.map((r) => (
                  <li key={r.id} className="flex gap-3 px-5 py-3 text-sm">
                    {r.actorName ? <Avatar id={r.actorId} name={r.actorName} className="mt-0.5" /> : <span aria-hidden className="size-7 shrink-0" />}
                    <div className="min-w-0 flex-1">
                      <div className="flex flex-wrap items-baseline gap-x-2">
                        <span className="font-semibold">{r.actorName ?? t('common.system')}</span>
                        <span className="text-muted-foreground">{t(`action.${r.action}`)}</span>
                        {r.itemKey && <Key>{r.itemKey}</Key>}<span className="break-words">{r.itemName}</span>
                        <time className="ml-auto whitespace-nowrap text-xs/[18px] text-muted-foreground tabular-nums" dateTime={r.occurredAt}>{fmtTime(r.occurredAt)}</time>
                      </div>
                      <div className="mt-0.5"><ChangeList row={r} /></div>
                    </div>
                  </li>
                ))}
              </ol>
            )}
            <div className="border-t px-5 py-2.5 text-sm"><Link className="text-primary underline underline-offset-4 hover:text-foreground" to={`${base}/activity`}>{t('dash.allActivity')}</Link></div>
          </Section>
        </>
      )}
      {creating && <CreateTask p={p} deliverables={lists.deliverables} milestones={lists.milestones} defaults={{ projectDisciplineId: disciplineId }}
        onClose={(id) => { setCreating(false); if (id) { q.refetch(); openPanel('Task', id) } }} />}
    </Page>
  )
}

/** Health, next milestone, next submission and phase (§13.1 item 3). Reported health leads with computed beside it, each
 *  labelled, and "Why?" lists the indicators behind the computed colour (§16.3, §16.4). */
function Headline() {
  const p = useCurrentProject()
  const h = p.health
  const tile = 'rounded-lg border bg-card px-5 py-4'
  const label = 'text-sm font-medium text-muted-foreground'
  return (
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      <div className={tile}>
        <p className={label}>{t('dash.health')}</p>
        <div className="mt-2 flex flex-wrap items-center gap-1.5">
          {h.overrideActive && <HealthPill health={h.reported} label={t('health.reported')} />}
          <Why reasons={h.reasons} title={t('health.whyTitle', { health: t(`health.${h.computed}`) })}><HealthPill health={h.computed} label={h.overrideActive ? t('health.computed') : undefined} /></Why>
        </div>
        {h.overrideActive && h.healthOverrideNote && <p className="mt-2 line-clamp-2 text-xs/[18px] text-muted-foreground" title={h.healthOverrideNote}>{h.healthOverrideNote}</p>}
      </div>
      {([['dash.nextMilestone', p.nextMilestone], ['dash.nextSubmission', p.nextSubmission]] as const).map(([key, m]) => (
        <div key={key} className={tile}>
          <p className={label}>{t(key)}</p>
          {m ? <>
            <p className="mt-2 break-words text-base/6 font-semibold"><span aria-hidden className="mr-1.5 text-muted-foreground">◆</span>{m.name}</p>
            <p className="text-sm text-muted-foreground tabular-nums">{fmtDate(m.date)} · {relative(m.date)}</p>
          </> : <p className="mt-2 text-sm text-muted-foreground">{t('common.none')}</p>}
        </div>
      ))}
      <div className={tile}>
        <p className={label}>{t('projects.col.phase')}</p>
        <p className="mt-2 break-words text-base/6 font-semibold">{p.phase ?? <Missing />}</p>
        <div className="mt-1.5"><StatusPill status={p.status} /></div>
      </div>
    </div>
  )
}

type Entry = [key: string, c: Count, to: string]
const BAD = ['overdue', 'blocked', 'atRisk', 'issuesHigh', 'risksHigh'], WARN = ['waiting', 'unassigned']

/** The counts row (§13.1 item 4) as summary tiles: every number opens the filtered list it was counted from. */
function Counts({ d, base }: { d: Dash; base: string }) {
  const tasks = (k: keyof Dash['tasks']): Entry => [k, d.tasks[k], `${base}/tasks?${d.tasks[k].link}`]
  const dels = (k: keyof Dash['deliverables']): Entry => [k, d.deliverables[k], `${base}/deliverables?${d.deliverables[k].link}`]
  const decs = (k: keyof Dash['decisions']): Entry => [`dec${k}`, d.decisions[k], `${base}/decisions?${d.decisions[k].link}`]
  const tile = (accent: Accent, title: string, [[key, c, to], ...rest]: Entry[]) => (
    <SummaryTile accent={accent} label={title} value={
      <Link to={to} className="inline-flex items-baseline gap-2 rounded-md hover:underline">{c.value}<span className="text-sm font-medium tracking-normal text-muted-foreground">{t(`dash.count.${key}`)}</span></Link>}>
      {rest.length > 0 && (
        <ul className="mt-3 grid grid-cols-2 gap-x-4 gap-y-1 border-t border-(color:--acc-stripe) pt-3">
          {rest.map(([k, n, href]) => {
            const tone = n.value > 0 && BAD.includes(k) ? 'bad' : n.value > 0 && WARN.includes(k) ? 'warn' : null
            return (
              <li key={k}>
                <Link to={href} className="flex min-h-6 items-baseline justify-between gap-2 rounded-md text-sm hover:underline">
                  <span className="text-muted-foreground">{t(`dash.count.${k}`)}</span>
                  <span className={cn('font-semibold tabular-nums', tone === 'bad' && 'text-bad', tone === 'warn' && 'text-warn')}>{tone && <span aria-hidden className="mr-1 text-xs">{tone === 'bad' ? '■' : '▲'}</span>}{n.value}</span>
                </Link>
              </li>
            )
          })}
        </ul>
      )}
    </SummaryTile>
  )
  return (
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      {tile('blue', t('ptab.tasks'), [tasks('total'), tasks('complete'), tasks('inProgress'), tasks('review'), tasks('overdue'), tasks('blocked'), tasks('waiting'), tasks('unassigned')])}
      {tile('mint', t('ptab.deliverables'), [dels('total'), dels('upcoming'), dels('atRisk'), dels('issued')])}
      {tile('lavender', t('ptab.decisions'), [decs('pending'), decs('overdue')])}
      {tile('peach', t('dash.registers'), [
        ['issuesOpen', { value: d.issues.open, link: '' }, `${base}/issues?indicator=open`],
        ['issuesHigh', { value: d.issues.high, link: '' }, `${base}/issues?indicator=open&severity=High`],
        ['risksHigh', { value: d.risks.high, link: '' }, `${base}/risks?indicator=open&severity=High`],
      ])}
    </div>
  )
}

function TaskListCard({ title, empty, data, to }: { title: string; empty: string; data: { items: TaskRow[]; total: number }; to: string }) {
  const openPanel = useItemPanel()
  return (
    <Section title={title} count={data.total} accent="blue"
      actions={data.total > data.items.length && <Link to={to} className="text-sm text-primary underline underline-offset-4 hover:text-foreground">{t('attention.showAll', { n: data.total })}</Link>}>
      {data.items.length === 0 ? <Empty>{empty}</Empty> : (
        <ul className="divide-y">
          {data.items.map((r) => (
            <li key={r.id} className="flex flex-wrap items-center gap-x-3 gap-y-1.5 px-5 py-3 text-sm">
              <Key>{r.key}</Key>
              <button type="button" className="min-w-0 flex-1 truncate text-left font-medium hover:underline" onClick={() => openPanel('Task', r.id)}>{r.name}</button>
              <span className="text-xs/[18px] text-muted-foreground">{r.assigneeName ?? t('ind.unassigned')}</span>
              <StatusPill status={r.status} />
              <span className={cn('whitespace-nowrap text-xs/[18px] tabular-nums', r.state?.isOverdue ? 'font-semibold text-bad' : 'text-muted-foreground')}>{t('workload.taskDue', { date: fmtDate(r.dueDate) })}</span>
              <TaskIndicators r={r} />
            </li>
          ))}
        </ul>
      )}
    </Section>
  )
}
