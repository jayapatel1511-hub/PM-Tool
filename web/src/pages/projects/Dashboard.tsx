import { PersonPanelTrigger } from '@/components/hub/person-panel-trigger'
import { ProjectStatusNote } from '@/components/hub/project-status-note'
import { useFlag } from '@/lib/auth'
import { useProjectRefresh } from '@/hooks/data'
import { useQuery } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { useState } from 'react'
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router'
import { type ActivityRow, ChangeList } from '@/components/hub/activity'
import { ScheduleNote, type PlannedTarget } from '@/components/hub/schedule-note'
import { AttentionPanel } from '@/components/hub/attention'
import { Empty, ErrorBanner, Loading, Missing, Page, Section, SummaryTile, TableRegion, selectCls, tdCls, thCls } from '@/components/hub/common'
import { TitleBlock } from '@/components/hub/title-block'
import { CountCard } from '@/components/hub/count-card'
import { useItemPanel } from '@/components/hub/panel-host'
import { Avatar } from '@/components/hub/people'
import { Chip, HealthPill, Key, StatusPill } from '@/components/hub/pills'
import { Why } from '@/components/hub/why'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import type { components } from '@/gen/api'
import { typedRequest } from '@/lib/api'
import { useOffer } from '@/lib/auth'
import { fmtDate, fmtTime, hours, relative } from '@/lib/format'
import { captureRecordContextOrigin } from '@/lib/record-context-navigation'
import { itemHref } from '@/components/hub/search'
import { t , runtimeText } from '@/lib/i18n'
import { cn, type Accent } from '@/lib/utils'
import { MilestoneStrip, type MilestoneRow } from './Milestones'
import { useCurrentProject } from './ProjectLayout'
import { CreateTask, TaskIndicators, useProjectLists, type TaskRow } from './Tasks'

type Count = components['schemas']['DashboardCount']
/** The dashboard's own shapes come from the contract; the milestone, task and activity rows keep the page types of the files that own them until those conversions land. */
interface Dash extends Omit<components['schemas']['DashboardView'], 'milestones' | 'dueThisWeek' | 'blocked' | 'activity'> {
  milestones: (Omit<MilestoneRow, 'isComplete' | 'isCancelled'> & { reasons?: unknown })[]
  dueThisWeek: { items: TaskRow[]; total: number; link: string }
  blocked: { items: TaskRow[]; total: number; link: string }
  activity: ActivityRow[]
}

// Section accents identify the section and repeat wherever it appears (design §3): tasks blue, disciplines mint, attention peach.
/** Project Dashboard (§13.1): state and what needs attention, each number opening the list behind it. */
export function DashboardTab() {
  const p = useCurrentProject()
  const statusNote = useFlag('project_status_note')
  const projectScreens = useFlag('project_screens')
  const visualSystem = useFlag('visual_system')
  const refresh = useProjectRefresh()
  const [sp, setSp] = useSearchParams()
  const [important, setImportant] = useState(false)
  const [creating, setCreating] = useState(false)
  const openPanel = useItemPanel()
  const lists = useProjectLists(p.id)
  const disciplineId = sp.get('discipline') ?? undefined
  const q = useQuery({ queryKey: ['p', p.id, 'dashboard', disciplineId, important], queryFn: async () => (await typedRequest('get', '/api/v1/projects/{id}/dashboard', { params: { id: p.id }, query: { disciplineId, importantOnly: important } })) as unknown as Dash })
  const base = `/projects/${p.projectNumber}`
  const lead = (id?: string | null) => p.disciplines.find((x) => x.leadUserId === id)?.leadName
  const snooze = useOffer()(p.permissions.snooze, p.status) // FR-FIG-08 decision 2
  return (
    <Page className={visualSystem ? 'visual-dashboard-band' : undefined} overlap={visualSystem && !!q.data && !q.error} title={t('ptab.dashboard')} titleBlock={q.data && <TitleBlock asOf={(q.data as Dash & { asOf?: string }).asOf} />} actions={<>
      <div className="flex items-center gap-2">
        <label htmlFor="dash-scope" className="text-sm font-medium">{t('dash.scope')}</label>
        <select id="dash-scope" className={cn(selectCls, 'w-auto max-w-56')} value={disciplineId ?? ''}
          onChange={(e) => { const n = new URLSearchParams(sp); if (e.target.value) n.set('discipline', e.target.value); else n.delete('discipline'); setSp(n, { replace: true }) }}>
          <option value="">{t('dash.allDisciplines')}</option>{p.disciplines.filter((d) => d.isActive).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
        </select>
      </div>
      {p.permissions.createTaskIn.length > 0 && <Button onClick={() => setCreating(true)}><Plus className="size-4" />{t('task.new')}</Button>}
    </>}>
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      {!q.data ? (q.isPending ? <Loading rows={8} /> : null) : (
        <>
          {/* §41 R9: the counts lead; the milestone strip follows at reduced height (less padding round its axis). */}
          <Counts d={q.data} base={base} scoped={!!disciplineId} />
          {statusNote && <ProjectStatusNote note={p.statusNote} edit={p.permissions.edit.ok ? { id: p.id, rowVersion: p.rowVersion, needsReason: p.permissions.needsReason, refresh: () => { refresh(p.id); q.refetch() } } : undefined} />}
          {q.data.milestones.length > 0 && <div className="[&>div>div]:py-2">
            <MilestoneStrip rows={q.data.milestones.map((m) => ({ ...m, isComplete: false, isCancelled: false }) as MilestoneRow)} onOpen={(m) => openPanel('Milestone', m.id)} />
          </div>}
          <Headline />
          {projectScreens && <StaffingUntilSubmission value={q.isFetching ? undefined : q.data.staffing} pending={q.isFetching} />}
          <ScheduleNote value={(p as typeof p & { latestPlanned?: PlannedTarget }).latestPlanned} />
          <div className="grid items-start gap-6 xl:grid-cols-[3fr_2fr]">
            <Section title={t('dash.attention')} id="attention" accent="peach">
              <AttentionPanel projectId={p.id} canSnooze={snooze.shown} refused={snooze.props.title} limit={10} disciplineId={disciplineId} severity={sp.get('severity') ?? undefined} groupByKind={projectScreens} />
            </Section>
            <Section title={t('dash.disciplines')} id="disciplines" accent="mint">
              {q.data.disciplines.length === 0 ? <Empty>{t('dash.notEvaluated')}</Empty> : (
                <TableRegion className="rounded-none border-0" role="region" aria-label={t('dash.disciplines')}>
                  <table className="w-full text-sm">
                    <thead className="bg-muted">
                      <tr>{([['common.discipline'], ['dash.lead'], ['common.status'], ...(q.data.disciplineStatus ? [['deliverable.progress']] as const : []), ['dash.open', true], ['ind.overdue', true], ['ind.blocked', true], ...(q.data.disciplineBacklog ? [['backlog.title', true]] as const : []), ['dash.due14', true], ['dash.nextDue']] as const)
                        .map(([h, num]) => <th key={h} scope="col" className={cn(thCls, num && 'text-right')}>{t(h)}</th>)}</tr>
                    </thead>
                    <tbody>
                      {q.data.disciplines.map((d) => {
                        const name = lead(d.leadId)
                        const tasks = `${base}/tasks?disciplineId=${d.disciplineId}`
                        const due14 = q.data.disciplineDue14?.[d.disciplineId]
                        const backlog = q.data.disciplineBacklog?.[d.disciplineId]
                        return (
                          <tr key={d.disciplineId} className="border-t hover:bg-muted">
                            <td className={tdCls}><Link className="font-semibold hover:underline" to={tasks}>{d.name}</Link></td>
                            <td className={cn(tdCls, 'whitespace-nowrap')}>
                              {name ? <span className="inline-flex items-center gap-2"><Avatar id={d.leadId} name={name} />{d.leadId ? <PersonPanelTrigger userId={d.leadId} name={name} focusKey={`dashboard:discipline:${d.disciplineId}:lead`} /> : name}</span> : <Chip tone="warn">{t('dash.noLead')}</Chip>}
                            </td>
                            <td className={tdCls}><Why reasons={d.reasons} title={d.name}><HealthPill health={d.health} /></Why></td>
                            {q.data.disciplineStatus && <td className={tdCls}><DisciplineStatus segments={q.data.disciplineStatus[d.disciplineId]} base={base} /></td>}
                            <td className={cn(tdCls, 'text-right tabular-nums')}><Link className="hover:underline" to={`${tasks}&open=true&onHold=false`}>{d.open}</Link></td>
                            <td className={cn(tdCls, 'text-right tabular-nums', d.overdue > 0 && 'font-semibold text-bad')}><Link className="hover:underline" to={`${tasks}&overdue=true`}>{d.overdue}</Link></td>
                            <td className={cn(tdCls, 'text-right tabular-nums', d.blocked > 0 && 'font-semibold text-bad')}><Link className="hover:underline" to={`${tasks}&blocked=true`}>{d.blocked}</Link></td>
                            {q.data.disciplineBacklog && <td className={cn(tdCls, 'text-right tabular-nums')}>{backlog && <Link className="hover:underline" to={`${base}/tasks?${backlog.link}`}>{backlog.value}</Link>}</td>}
                            <td className={cn(tdCls, 'text-right tabular-nums')}>{due14
                              ? <Link className="hover:underline" to={`${base}/deliverables?${due14.link}`}>{due14.value}</Link> : d.deliverablesDue14}</td>
                            <td className={cn(tdCls, 'whitespace-nowrap')} title={d.nextDueName ?? undefined}>{d.nextDueKey ? <><Key>{d.nextDueKey}</Key> <span className="tabular-nums">{fmtDate(d.nextDueDate)}</span></> : <Missing />}</td>
                          </tr>
                        )
                      })}
                    </tbody>
                  </table>
                </TableRegion>
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
                        <span className="font-semibold">{r.actorId && r.actorName ? <PersonPanelTrigger userId={r.actorId} name={r.actorName} focusKey={`dashboard:activity:${r.id}:actor`} /> : r.actorName ?? t('common.system')}</span>
                        <span className="text-muted-foreground">{runtimeText(`action.${r.action}`)}</span>
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
          <Why reasons={h.reasons} title={t('health.whyTitle', { health: runtimeText(`health.${h.computed}`) })}><HealthPill health={h.computed} label={h.overrideActive ? t('health.computed') : undefined} /></Why>
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

/** The counts row (§13.1 item 4) as summary tiles: every number opens the filtered list it was counted from, and each tile
 *  says what it counts (AC-CNT-06). Decisions, issues and risks are project-wide, so a discipline scope says so (AC-CNT-07). */
function Counts({ d, base, scoped }: { d: Dash; base: string; scoped: boolean }) {
  const visualSystem = useFlag('visual_system')
  const project = useCurrentProject()
  const [sp] = useSearchParams()
  const tasks = (k: keyof Dash['tasks']): Entry => [k, d.tasks[k]!, `${base}/tasks?${d.tasks[k]!.link}`]
  const dels = (k: keyof Dash['deliverables']): Entry => [k, d.deliverables[k], `${base}/deliverables?${d.deliverables[k].link}`]
  const decs = (k: keyof Dash['decisions']): Entry => [`dec${k}`, d.decisions[k], `${base}/decisions?${d.decisions[k].link}`]
  if (visualSystem) {
    const selected = project.disciplines.find(discipline => discipline.id === sp.get('discipline'))
    const where = (whole = false) => t(whole ? 'dashboardScreens.whereWhole' : scoped ? 'dashboardScreens.whereDiscipline' : 'dashboardScreens.whereProject', {
      project: project.projectNumber, discipline: selected?.name ?? t('common.unavailable'),
    })
    const primary = [
      { key: 'inProgress', count: d.tasks.inProgress, path: 'tasks', tone: 'violet', whole: false },
      { key: 'review', count: d.tasks.review, path: 'tasks', tone: 'amber', whole: false },
      { key: 'overdue', count: d.tasks.overdue, path: 'tasks', tone: 'red', whole: false },
      { key: 'upcoming', count: d.deliverables.upcoming, path: 'deliverables', tone: 'amber', whole: false },
      { key: 'decpending', count: d.decisions.pending, path: 'decisions', tone: 'violet', whole: true },
      { key: 'issuesOpen', count: d.registers.issuesOpen, path: 'issues', tone: 'violet', whole: true },
    ] as const
    const siblings: { title: string; whole: boolean; values: Entry[] }[] = [
      { title: t('ptab.tasks'), whole: false, values: [tasks('total'), tasks('complete'), tasks('blocked'), tasks('waiting'), tasks('unassigned'), ...(d.tasks.backlog ? [tasks('backlog')] : [])] },
      { title: t('ptab.deliverables'), whole: false, values: [dels('total'), dels('atRisk'), dels('issued')] },
      { title: t('ptab.decisions'), whole: true, values: [decs('overdue')] },
      { title: t('dash.registers'), whole: true, values: [
        ['issuesHigh', d.registers.issuesHigh, `${base}/issues?${d.registers.issuesHigh.link}`],
        ['risksHigh', d.registers.risksHigh, `${base}/risks?${d.registers.risksHigh.link}`],
      ] },
    ]
    return <div className="space-y-4" data-dashboard-counts>
      <div className="home-count-cards">{primary.map(card => <CountCard key={card.key} label={t(`dashboardScreens.card.${card.key}`)} value={card.count.value}
        tone={card.tone} definition={t(`dashboardScreens.definition.${card.key}`)} where={where(card.whole)} to={`${base}/${card.path}?${card.count.link}`} />)}</div>
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4" aria-label={t('dashboardScreens.otherCounts')}>{siblings.map(group => <section key={group.title} className="rounded border bg-card p-4">
        <h3 className="mb-2 text-sm font-medium">{group.title}{group.whole && scoped && <span className="ml-2 text-xs text-muted-foreground">{t('sig.wholeProject')}</span>}</h3>
        <ul className="space-y-1">{group.values.map(([key, count, href]) => <li key={key}><Link className="flex min-h-7 items-baseline justify-between gap-3 rounded text-sm hover:underline" to={href}>
          <span>{runtimeText(`dash.count.${key}`)}</span><span className="font-medium tabular-nums">{count.value}</span>
        </Link></li>)}</ul>
      </section>)}</div>
    </div>
  }
  const tile = (accent: Accent, title: string, hint: string, whole: boolean, [[key, c, to], ...rest]: Entry[]) => (
    <article aria-label={title} className="grid">
      <SummaryTile accent={accent} hint={hint} to={to}
        label={<>{title}{whole && scoped && <Chip tone="idle">{t('sig.wholeProject')}</Chip>}</>}
        value={<span className="inline-flex items-baseline gap-2">{c.value}<span className="text-sm font-medium tracking-normal text-muted-foreground">{runtimeText(`dash.count.${key}`)}</span></span>}>
        {rest.length > 0 && (
          <ul className="mt-3 grid grid-cols-2 gap-x-4 gap-y-1 border-t border-(color:--acc-stripe) pt-3">
            {rest.map(([k, n, href]) => {
              const tone = n.value > 0 && BAD.includes(k) ? 'bad' : n.value > 0 && WARN.includes(k) ? 'warn' : null
              return (
                <li key={k}>
                  <Link to={href} className="flex min-h-6 items-baseline justify-between gap-2 rounded-md text-sm hover:underline">
                    <span className="text-muted-foreground">{runtimeText(`dash.count.${k}`)}</span>
                    <span className={cn('font-semibold tabular-nums', tone === 'bad' && 'text-bad', tone === 'warn' && 'text-warn')}>{tone && <span aria-hidden className="mr-1 text-xs">{tone === 'bad' ? '■' : '▲'}</span>}{n.value}</span>
                  </Link>
                </li>
              )
            })}
          </ul>
        )}
      </SummaryTile>
    </article>
  )
  return (
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      {tile('blue', t('ptab.tasks'), t('sig.def.dashTasks'), false, [tasks('total'), tasks('complete'), tasks('inProgress'), tasks('review'), tasks('overdue'), tasks('blocked'), tasks('waiting'), tasks('unassigned'), ...(d.tasks.backlog ? [tasks('backlog')] : [])])}
      {tile('mint', t('ptab.deliverables'), t('sig.def.dashDeliverables'), false, [dels('total'), dels('upcoming'), dels('atRisk'), dels('issued')])}
      {tile('lavender', t('ptab.decisions'), t('sig.def.dashDecisions'), true, [decs('pending'), decs('overdue')])}
      {tile('peach', t('dash.registers'), t('sig.def.dashRegisters'), true, [
        ['issuesOpen', d.registers.issuesOpen, `${base}/issues?${d.registers.issuesOpen.link}`],
        ['issuesHigh', d.registers.issuesHigh, `${base}/issues?${d.registers.issuesHigh.link}`],
        ['risksHigh', d.registers.risksHigh, `${base}/risks?${d.registers.risksHigh.link}`],
      ])}
    </div>
  )
}

function StaffingUntilSubmission({ value, pending }: { value?: Dash['staffing']; pending: boolean }) {
  const openPanel = useItemPanel(), location = useLocation(), navigate = useNavigate(), project = useCurrentProject()
  const target = value?.target
  const measure = (total: number | null | undefined, known: number) => <div className="space-y-1 tabular-nums">
    <p>{total == null ? t('common.unavailable') : hours(total)}</p>
    {total == null && <p className="text-xs text-muted-foreground">{t('dashboardScreens.knownHours', { hours: hours(known) })}</p>}
  </div>
  return <Section id="dashboard-staffing" title={t('dashboardScreens.staffing')}>
    {pending ? <Loading rows={3} /> : !value ? <p className="p-5 text-sm">{t('dashboardScreens.staffingUnavailable')}</p> : <div className="space-y-3 p-5 text-sm">
      <p>{target ? <><span>{t(target.kind === 'SubmissionPackage' ? 'dashboardScreens.submissionBasis' : 'dashboardScreens.milestoneBasis')} </span>{target.kind === 'SubmissionPackage'
        ? <Link className="underline underline-offset-4" to={itemHref('SubmissionPackage', project.projectNumber, target.id)} onClick={event => {
          if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return
          event.preventDefault()
          navigate(itemHref('SubmissionPackage', project.projectNumber, target.id), { state: { recordContextReturn: captureRecordContextOrigin(location, event.currentTarget) } })
        }}><Key>{target.key}</Key> {target.name}</Link>
        : <button className="underline underline-offset-4" onClick={event => openPanel('Milestone', target.id, captureRecordContextOrigin(location, event.currentTarget))}><Key>{target.key}</Key> {target.name}</button>}</> : t('dashboardScreens.noTarget')}</p>
      {value.state !== 'NoTarget' && value.state !== 'OverdueTarget' && <p className="text-xs text-muted-foreground">{t('dashboardScreens.staffingWindow', { from: fmtDate(value.fromDate), through: fmtDate(value.throughDate) })}</p>}
      {value.state === 'OverdueTarget' ? <p>{t('dashboardScreens.overdueTarget')}</p> : value.state === 'NoTarget' ? <p>{t('dashboardScreens.staffingUnavailable')}</p> : <>
        <p className="text-xs text-muted-foreground">{t('dashboardScreens.staffingDefinition')}</p>
        {value.allocationCoverage !== 'Complete' && <p>{t('dashboardScreens.visibleAllocations')}</p>}
        {(value.unattributedConfirmedHours == null || (value.knownUnattributedConfirmedHours ?? 0) > 0 || (value.unavailableUnattributed ?? 0) > 0) && <p>{t('dashboardScreens.unattributedReservations', { hours: hours(value.knownUnattributedConfirmedHours ?? 0) })}</p>}
        {!value.disciplines.length ? <Empty>{t('dashboardScreens.noDisciplines')}</Empty> : <TableRegion className="rounded-none border-0" role="region" aria-label={t('dashboardScreens.staffing')}>
          <table className="w-full text-sm"><caption className="sr-only">{t('dashboardScreens.staffing')}</caption><thead className="bg-muted"><tr>
            <th className={thCls} scope="col">{t('common.discipline')}</th><th className={thCls} scope="col">{t('dashboardScreens.remaining')}</th><th className={thCls} scope="col">{t('dashboardScreens.confirmed')}</th><th className={thCls} scope="col">{t('dashboardScreens.openRoles')}</th><th className={thCls} scope="col">{t('dashboardScreens.comparison')}</th>
          </tr></thead><tbody>{value.disciplines.map(row => <tr key={row.projectDisciplineId ?? 'unattributed'} className="border-t" data-dashboard-staffing={row.projectDisciplineId ?? 'unattributed'}>
            <th className={cn(tdCls, 'text-left font-medium')} scope="row">{row.name ?? t('dashboardScreens.unattributed')}</th>
            <td className={tdCls}>{measure(row.remainingHours, row.knownRemainingHours)}{row.unestimatedTasks > 0 && <p className="text-xs text-muted-foreground">{t('dashboardScreens.unestimated', { n: row.unestimatedTasks })}</p>}{row.undatedTasks > 0 && <p className="text-xs text-muted-foreground">{t('dashboardScreens.undated', { n: row.undatedTasks })}</p>}</td>
            <td className={tdCls}>{measure(row.confirmedHours, row.knownConfirmedHours)}{row.unavailableConfirmed > 0 && <p className="text-xs text-muted-foreground">{t('dashboardScreens.unavailableReservations', { n: row.unavailableConfirmed })}</p>}</td>
            <td className={tdCls}>{measure(row.openRoleHours, row.knownOpenRoleHours)}{row.unavailableOpenRoles > 0 && <p className="text-xs text-muted-foreground">{t('dashboardScreens.unavailableRoles', { n: row.unavailableOpenRoles })}</p>}</td>
            <td className={tdCls}>{row.excessHours == null ? t('dashboardScreens.comparisonUnavailable') : row.excessHours > 0 ? t('dashboardScreens.excess', { hours: hours(row.excessHours) }) : t('dashboardScreens.noExcess')}</td>
          </tr>)}</tbody></table>
        </TableRegion>}
      </>}
    </div>}
  </Section>
}

function TaskListCard({ title, empty, data, to }: { title: string; empty: string; data: { items: TaskRow[]; total: number }; to: string }) {
  const openPanel = useItemPanel()
  const visualSystem = useFlag('visual_system')
  return (
    <Section title={title} count={data.total} accent="blue"
      actions={data.total > data.items.length && <Link to={to} className="text-sm text-primary underline underline-offset-4 hover:text-foreground">{t('attention.showAll', { n: data.total })}</Link>}>
      {data.items.length === 0 ? <Empty>{empty}</Empty> : (
        <ul className="divide-y">
          {data.items.map((r) => (
            <li key={r.id} className="flex flex-wrap items-center gap-x-3 gap-y-1.5 px-5 py-3 text-sm">
              <Key>{r.key}</Key>
              <button type="button" className={cn('min-w-0 grow basis-48 text-left font-medium hover:underline', visualSystem ? 'whitespace-normal break-words' : 'truncate')} onClick={() => openPanel('Task', r.id)}>{r.name}</button>
              <span className="text-xs/[18px] text-muted-foreground">{r.assigneeId && r.assigneeName ? <PersonPanelTrigger userId={r.assigneeId} name={r.assigneeName} focusKey={`dashboard:${to}:task:${r.id}:assignee`} /> : r.assigneeName ?? t('ind.unassigned')}</span>
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

/** Fixed status groups share the register's server links; each remains readable without colour. */
function DisciplineStatus({ segments, base }: { segments?: Record<'notStarted' | 'inProgress' | 'issued' | 'onHold', Count>; base: string }) {
  const keys = ['notStarted', 'inProgress', 'issued', 'onHold'] as const
  const total = keys.reduce((sum, key) => sum + (segments?.[key].value ?? 0), 0)
  if (!total || !segments) return <span className="text-xs text-muted-foreground">{t('rcv.noDeliverables')}</span>
  const colours = ['bg-idle', 'bg-primary', 'bg-ok', 'bg-warn']
  return <div className="min-w-32 space-y-1"><div className="hidden h-3 overflow-hidden rounded sm:flex">{keys.map((key, index) => <Link key={key} to={`${base}/deliverables?${segments[key].link}`} aria-label={`${t(`rcv.${key}`)}: ${segments[key].value}`} title={`${t(`rcv.${key}`)}: ${segments[key].value}`} className={colours[index]} style={{ width: `${100 * segments[key].value / total}%` }} />)}</div><span className="text-xs tabular-nums">{t('rcv.issuedOf', { n: segments.issued.value, m: total })}</span></div>
}
