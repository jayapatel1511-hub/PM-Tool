import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowDown, ArrowUp, CalendarRange, Eye, EyeOff, Pencil, RotateCcw } from 'lucide-react'
import { Fragment, useEffect, useRef, useState, type CSSProperties, type ReactNode } from 'react'
import { Link, useLocation, useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { Empty, ErrorBanner, Field, Loading, Missing, Notice, Page, PageHelp, Section, SummaryTile, selectCls } from '@/components/hub/common'
import { TitleBlock } from '@/components/hub/title-block'
import { CountCard } from '@/components/hub/count-card'
import { IdentityDot, ItemMark } from '@/components/hub/identity-colour'
import { HomeDeadlineRunway, HomeStatusDonut } from '@/components/hub/home-visual-charts'
import { HomeWeeklyCapacity } from '@/components/hub/home-visual-capacity'
import { HomeTrendChip, HomeTrendDefinition, HomeTrendInspection, HomeTrendReferences } from '@/components/hub/home-visual-trends'
import { useItemPanel } from '@/components/hub/panel-host'
import { PersonPanelTrigger } from '@/components/hub/person-panel-trigger'
import { Chip, Key, StatusPill } from '@/components/hub/pills'
import { HOME_PRESETS, scoped, useScope, WorkspaceSelector, WorkspaceTabs, type Scope } from '@/components/hub/workspace'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import type { components } from '@/gen/api'
import { ApiError, typedRequest } from '@/lib/api'
import { useFlag, useMe } from '@/lib/auth-context'
import { fmtDate, fmtTime, relative } from '@/lib/format'
import { refreshHome } from '@/lib/keys'
import { captureRecordContextOrigin } from '@/lib/record-context-navigation'
import { plural, t, tv, runtimeText, textFor } from '@/lib/i18n'
import { cn } from '@/lib/utils'

type Schemas = components['schemas']
type Lane = Schemas['HomeLane']
type Widget = Schemas['Widget']
type HomeData = Schemas['HomeRead']


/** The same four lanes as the board (§36.3), coloured with text beside every bar. */
const LANE_TONE: Record<string, string> = { 'To Do': 'var(--lane-todo, var(--idle))', 'In Progress': 'var(--lane-progress, var(--work))', Review: 'var(--lane-review, var(--warn))', Done: 'var(--lane-done, var(--done))' }
/** Home counts every Complete task as Done; the board's Done lane shows only the last 14 days. Home's Review lane also
 *  holds Revision Required, so it is named for both (§40 D9). */
const laneName = (lane: string) => (lane === 'Done' ? t('home.laneDone') : lane === 'Review' ? t('sig.laneReview') : runtimeText(`lane.${lane}`))
/** A lane's definition: the canonical statuses it counts, as the server reports them. */
const laneDef = (l: Lane) => `${laneName(l.lane)}: ${l.statuses.split(',').map((x) => tv(x)).join(', ')}`

/** Project-list links for a scope: its own "mine" default, everything permitted, or the chosen projects. */
function projectsLink(scope: Scope, extra: Record<string, string>, preset?: string, empty = false) {
  const p = new URLSearchParams({ status: 'Active', ...extra })
  if (preset) { p.set('mine', 'false'); p.set('homePreset', preset); p.set('homeProjects', scope.api); if (empty) p.set('ids', 'none'); return `/projects?${p}` }
  if (scope.kind !== 'mine') p.set('mine', 'false')
  if (empty) p.set('ids', 'none'); else if (scope.ids) p.set('ids', scope.ids.join(',') || 'none')
  return `/projects?${p}`
}

/** Home overview (§36.6, FR-VIS-07): current totals for the selected projects, each opening the list behind it; the
 *  date range changes only Upcoming Deadlines. Each person may reorder or hide the fixed widgets and restore the default. */
export function HomePage() {
  const me = useMe()
  const visualSystem = useFlag('visual_system')
  const homeOverview = useFlag('home_overview')
  const scope = useScope()
  const [sp, setSp] = useSearchParams()
  const qc = useQueryClient()
  const openPanel = useItemPanel()
  const location = useLocation()
  const from = sp.get('from') ?? undefined, to = sp.get('to') ?? undefined
  const projectStatus = homeOverview ? sp.get('projectStatus') ?? undefined : undefined
  const homePreset = homeOverview ? sp.get('homePreset') ?? undefined : undefined
  const deadlinesOnly = homeOverview && sp.get('view') === 'deadlines'
  const deadlinePage = deadlinesOnly ? Math.max(1, Math.min(2147483647, Number.parseInt(sp.get('deadlinePage') ?? '1', 10) || 1)) : undefined
  const [statusFilterOpen, setStatusFilterOpen] = useState(false)
  const [presetOpen, setPresetOpen] = useState(false)
  const [presetSaving, setPresetSaving] = useState(false)
  const [comparing, setComparing] = useState(false)
  const restoredPreset = useRef(false)
  const key = ['home', scope.api, from, to, projectStatus, deadlinePage, homePreset]
  const q = useQuery({ queryKey: key, enabled: scope.ready, queryFn: () => typedRequest('get', '/api/v1/home', { query: { projects: scope.api, from, to, projectStatus, deadlinePage, homePreset } }) })
  const explicitScope = ['projects', 'ws', 'projectStatus', 'homePreset'].some(key => sp.has(key))
  const restoring = homeOverview && !restoredPreset.current && !explicitScope && (q.isFetching || !q.isFetchedAfterMount || !!q.data?.preset?.savedPreset)
  useEffect(() => {
    if (!homeOverview || !q.data?.preset || restoredPreset.current || q.isFetching || !q.isFetchedAfterMount) return
    restoredPreset.current = true
    if (!explicitScope && q.data.preset.savedPreset) {
      const next = new URLSearchParams(sp); next.delete('ws'); next.set('projects', 'all'); next.set('homePreset', q.data.preset.savedPreset); next.delete('deadlinePage')
      setSp(next, { replace: true })
    }
  }, [homeOverview, q.data?.preset, q.isFetching, q.isFetchedAfterMount, explicitScope, sp, setSp])
  const [editing, setEditing] = useState(false)
  const set = (k: string, v?: string) => { const n = new URLSearchParams(sp); if (k !== 'deadlinePage') n.delete('deadlinePage'); if (v) n.set(k, v); else n.delete(k); setSp(n, { replace: true }) }
  const clearRange = () => { const n = new URLSearchParams(sp); n.delete('from'); n.delete('to'); n.delete('deadlinePage'); setSp(n, { replace: true }) }

  const saveLayout = async (widgets: Widget[]) => {
    qc.setQueryData<HomeData>(key, (d) => d && { ...d, layout: widgets, isDefault: false })
    try { await typedRequest('put', '/api/v1/me/dashboard-layout', { body: { widgets } }) } catch (e) { toast.error((e as ApiError).message) }
    refreshHome(qc)
  }
  const restore = async () => {
    const r = await typedRequest('delete', '/api/v1/me/dashboard-layout', {})
    qc.setQueryData<HomeData>(key, (d) => d && { ...d, layout: r.layout, isDefault: true })
    refreshHome(qc)
    toast.success(t('home.restored'))
  }

  const choosePreset = async (code: string | null) => {
    if (presetSaving) return
    setPresetSaving(true)
    restoredPreset.current = true
    const next = new URLSearchParams(sp); next.delete('deadlinePage')
    if (code) { next.delete('ws'); next.set('projects', 'all'); next.set('homePreset', code) } else next.delete('homePreset')
    setSp(next)
    try { await typedRequest('put', '/api/v1/me/home-preset', { body: { code } }); refreshHome(qc) }
    catch { toast.error(t('home.preset.notSaved')) }
    finally { setPresetSaving(false) }
  }
  const d = restoring ? undefined : q.data
  const context = homeOverview ? d?.countContext : undefined
  const preset = homeOverview ? d?.preset : undefined
  const trends = homeOverview ? d?.trends : undefined
  const trend = (id: string) => trends?.metrics.find(metric => metric.id === id)
  const drillScope: Scope = homeOverview ? { ...scope, params: { ...scope.params, ...(homePreset ? { homePreset } : {}), ...(projectStatus ? { homeProjectStatus: projectStatus } : {}) } } : scope
  const emptyActive = !!context?.projectStatuses.length && !context.projectStatuses.includes('Active')
  const projectLink = (extra: Record<string, string>) => projectsLink(scope, extra, homePreset, emptyActive)
  const presetProjectsLabel = preset?.code === 'ProjectManager' ? t('home.preset.projectManagerProjects') : preset?.code === 'DisciplineLead' ? t('home.preset.disciplineLeadProjects') : preset?.code === 'Supervisor' ? t('home.preset.supervisorProjects') : undefined
  const body = (w: Widget): ReactNode => {
    if (!d) return null
    const m = d.metrics
    switch (w.id) {
      // Tiles in the Home order (blue, mint, lavender, peach); a warning travels as a labelled chip, never as the tint.
      // Each tile is a labelled data card (article), as on the project dashboard (§41 R9).
      case 'metrics': {
        const ratio = m.onTrack.evaluated === 0 ? t('home.onTrackNone') : <>
          <Link className="text-primary underline underline-offset-4" to={projectLink({ computedHealth: 'Green' })}>{t('home.onTrackGreen', { n: m.onTrack.green })}</Link>
          {' / '}
          <Link className="text-primary underline underline-offset-4" to={projectLink({ computedHealth: 'Green,Yellow,Red' })}>{t('home.onTrackEvaluated', { n: m.onTrack.evaluated })}</Link>
        </>
        const projectWhere = (which: 'openTasksProject' | 'overdueTasksProject') => context
          ? context[which] ? t('home.topProject', { n: context[which]!.count, number: context[which]!.projectNumber }) : t('home.noMatchingTasks')
          : undefined
        const metric = ({ accent, trendId, ...props }: React.ComponentProps<typeof CountCard> & { accent: 'blue' | 'mint' | 'lavender' | 'peach' | 'rose'; trendId?: string }) => {
          const item = trendId ? trend(trendId) : undefined
          const definition = item ? <HomeTrendDefinition definition={props.definition} metric={item} trends={trends} /> : props.definition
          return visualSystem ? <CountCard {...props} definition={definition} trend={item ? <HomeTrendChip metric={item} /> : undefined} where={props.where ?? scope.label} />
            : <article aria-label={String(props.label)} className="grid"><SummaryTile label={props.label} value={props.value} accent={accent} hint={definition} to={props.to}>
              {props.where && <span className="mt-1 block text-xs text-muted-foreground">{props.where}</span>}{item && <HomeTrendChip metric={item} />}{props.children}
            </SummaryTile></article>
        }
        const busiest = context?.busiestDueDate ? new Intl.DateTimeFormat('en-GB', { weekday: 'short', day: 'numeric', month: 'short', timeZone: 'UTC' })
          .format(new Date(`${context.busiestDueDate}T00:00:00Z`)).replace(',', '') : null
        return <section aria-labelledby="h-metrics">
          <h2 id="h-metrics" className="sr-only">{t('home.metrics')}</h2>
          <div className={cn('grid grid-cols-2 gap-3 sm:grid-cols-3 sm:gap-4', visualSystem && 'home-count-cards', homeOverview ? 'xl:grid-cols-6' : 'xl:grid-cols-5')} style={{ '--count-columns': context ? 6 : 5 } as CSSProperties}>
            {metric({ label: t('home.activeProjects'), value: m.activeProjects, accent: 'blue', tone: 'blue', trendId: 'ActiveProjects', definition: t('sig.def.activeProjects'),
              where: context ? t('home.projectDenominator', { n: context.projectsInScope }) : undefined, to: projectLink({}) })}
            {metric({ label: t('home.openTasks'), value: m.openTasks, accent: 'mint', tone: 'violet', trendId: 'OpenTasks', definition: t('sig.def.openTasks'),
              where: projectWhere('openTasksProject'), to: scoped(drillScope, '/tasks', { open: 'true' }) })}
            {metric({ label: t('home.overdueTasks'), value: m.overdueTasks, accent: 'lavender', tone: 'red', trendId: 'OverdueTasks', definition: t('sig.def.overdueTasks'),
              where: projectWhere('overdueTasksProject'), to: scoped(drillScope, '/tasks', { overdue: 'true' }),
              children: !visualSystem && m.overdueTasks > 0 ? <span className="mt-1.5 block"><Chip tone="bad">{t('dash.attention')}</Chip></span> : undefined })}
            {context && <CountCard label={t('home.dueWithin', { n: context.dueWithinDays })} value={context.dueWithinTasks + context.dueWithinDeliverables + context.dueWithinMilestones}
              where={busiest ? t('home.busiestDue', { n: context.busiestDueCount, date: busiest }) : t('home.noWindowDeadlines')}
              definition={<HomeTrendDefinition definition={t('home.dueDefinition', { n: context.dueWithinDays })} metric={trend('DueWithin')} trends={trends} />} tone="amber" trend={<HomeTrendChip metric={trend('DueWithin')} />}
              to={`${scoped(drillScope, '/home', { from: context.dueFrom, to: context.dueTo, projectStatus, view: 'deadlines' })}#h-deadlines`}>
              <span className="count-card__where">{t('home.dueSplit', { tasks: context.dueWithinTasks, deliverables: context.dueWithinDeliverables, milestones: context.dueWithinMilestones })}</span>
            </CountCard>}
            {metric({ label: t('home.onTrack'), value: m.onTrack.pct == null ? <Missing /> : `${m.onTrack.pct}%`,
              valueText: m.onTrack.pct == null ? t('common.unavailable') : undefined, accent: 'peach', tone: 'green', trendId: 'OnTrack', definition: t('sig.def.onTrack'), where: ratio,
              children: context && context.onTrackCriticalProjects.length > 0 ? <span className="count-card__where">{t('home.criticalProjects')}: {context.onTrackCriticalProjects.map((number, i) => <span key={number}>{i > 0 && ', '}<Link className="underline underline-offset-2" to={`/projects/${encodeURIComponent(number)}/dashboard`}>{number}</Link></span>)}</span> : undefined })}
            {metric({ label: t('home.teamMembers'), value: m.teamMembers, accent: 'rose', tone: 'teal', definition: t('sig.def.teamMembers'),
              where: context ? t('home.acrossDisciplines', { n: context.homeDisciplineCount }) : undefined, to: scoped(drillScope, '/team') })}
          </div>
        </section>
      }
      case 'tasksByStatus': {
        // AC-CNT-05: On Hold is its own row, so the open lanes plus On hold add up to Open Tasks.
        const lanes = [...d.tasksByStatus.map((l) => ({ ...l, name: laneName(l.lane), def: laneDef(l), tone: LANE_TONE[l.lane] })),
          { lane: 'On Hold', statuses: d.onHold.status, count: d.onHold.count, name: t('lane.On Hold'), def: `${t('lane.On Hold')}: ${tv(d.onHold.status)}`, tone: homeOverview ? 'var(--time-away-hatch, repeating-linear-gradient(135deg, #94a0b4 0 3px, #d5dae3 3px 6px))' : 'var(--time-away-hatch, var(--idle))' }]
        const max = Math.max(1, ...lanes.map((l) => l.count))
        return (
          <Section id="h-status" accent="blue" title={t('home.byStatus')} actions={<PageHelp label={t('sig.laneHelp')}>
            <ul className="space-y-1">{lanes.map((l) => <li key={l.lane}>{l.def}</li>)}</ul>
            <p className="mt-2">{t('sig.laneSum')}</p>
          </PageHelp>}>
            {homeOverview ? <HomeStatusDonut lanes={lanes} scope={drillScope} /> : <ul className="space-y-1 px-5 py-4">
              {lanes.map((l) => (
                <li key={l.lane}>
                  <Link to={scoped(drillScope, '/tasks', { status: l.statuses })} aria-label={t('home.segment', { lane: l.name, n: l.count })} title={l.def}
                    className="grid grid-cols-[10rem_1fr_3rem] items-center gap-3 rounded-md py-0.5 text-sm hover:bg-muted">
                    <span>{l.name}</span>
                    <span className="block h-4 rounded-full bg-secondary"><span data-home-lane={l.lane} className="block h-full rounded-full" style={{ width: `${(l.count / max) * 100}%`, background: l.tone }} /></span>
                    <span className="text-right font-medium tabular-nums">{l.count}</span>
                  </Link>
                </li>
              ))}
            </ul>}
            {context && <div className="border-t px-5 py-3 text-sm">
              <p>{t('home.openCheck', { parts: d.tasksByStatus.filter(l => l.lane !== 'Done').map(l => l.count).join(' + '), hold: d.onHold.count, total: m.openTasks })}</p>
              <Link className="mt-1 inline-block text-primary underline underline-offset-2" to={scoped(drillScope, '/tasks', { open: 'true', withoutDueDate: 'true' })}>{t('home.undatedCount', { n: context.openTasksWithoutDueDate })}</Link>
            </div>}
          </Section>
        )
      }
      case 'tasksByProject': return <ByProject d={d} scope={drillScope} overview={homeOverview} />
      case 'weeklyCapacity': return homeOverview && me.capabilities.workload && d.weeklyCapacity ? <HomeWeeklyCapacity capacity={d.weeklyCapacity} /> : null
      case 'upcomingDeadlines': return (
        <Section id="h-deadlines" accent="peach" title={t('home.deadlines')} actions={
          <Popover>
            <PopoverTrigger asChild>
              <Button variant="outline" size="sm" aria-label={t('sig.rangeLabel', { range: t('home.range', { from: fmtDate(d.from), to: fmtDate(d.to) }) })}>
                <CalendarRange className="size-4" aria-hidden /><span className="tabular-nums">{t('home.range', { from: fmtDate(d.from), to: fmtDate(d.to) })}</span>
              </Button>
            </PopoverTrigger>
            <PopoverContent align="end" aria-label={t('sig.range')} className="w-80 space-y-3">
              <Field label={t('common.from')} htmlFor="h-from"><Input id="h-from" type="date" value={from ?? d.from} onChange={(e) => set('from', e.target.value || undefined)} /></Field>
              <Field label={t('common.to')} htmlFor="h-to"><Input id="h-to" type="date" value={to ?? d.to} onChange={(e) => set('to', e.target.value || undefined)} /></Field>
              {(from || to) && <Button variant="link" size="sm" className="px-0" onClick={clearRange}>{t('filters.clear')}</Button>}
            </PopoverContent>
          </Popover>}>
          {homeOverview && !deadlinesOnly && <HomeDeadlineRunway d={d} scope={drillScope} projectStatus={projectStatus} openItem={(kind, id, source) => openPanel(kind, id, captureRecordContextOrigin(location, source))} />}
          {d.upcomingDeadlines.items.length === 0
            ? <Empty action={(from || to) && <Button variant="outline" onClick={clearRange}>{t('filters.clear')}</Button>}>{t('home.noDeadlines')}</Empty> : (
            <ul className="divide-y">
              {d.upcomingDeadlines.items.map((x) => (
                <li key={`${x.type}-${x.id}`}>
                  <button type="button" className="flex min-h-(--row-min) w-full flex-wrap items-center gap-x-3 gap-y-1 px-5 py-(--cell-py) text-left text-sm hover:bg-muted" onClick={e => openPanel(x.type, x.id, homeOverview ? captureRecordContextOrigin(location, e.currentTarget) : undefined)}>
                    <span className={cn('w-24 shrink-0 tabular-nums', x.overdue && 'font-semibold text-bad')}>{fmtDate(x.date)}</span>
                    <span className="w-24 shrink-0 text-xs/[18px] text-muted-foreground">{runtimeText(`itemType.${x.type}`)}</span>
                    <span className="flex min-w-[10rem] flex-1 basis-full flex-wrap items-center gap-2 sm:basis-auto sm:flex-nowrap"><ItemMark kind={x.type === 'Task' || x.type === 'Deliverable' || x.type === 'Milestone' ? x.type : undefined} colour={x.identityColour} fallback={<IdentityDot id={x.projectId} colour={x.identityColour} />} /><span className="shrink-0 whitespace-nowrap"><Key>{x.key}</Key></span><span className="w-full min-w-0 break-words font-medium sm:w-auto sm:truncate">{x.name}</span></span>
                    {x.owner && <span className="text-xs/[18px] text-muted-foreground">{x.owner}</span>}
                    {homeOverview && x.note && Number.isInteger(x.note.days) && x.note.days >= 0 && ['InReview', 'LaterThanBaseline'].includes(x.note.kind) && <span data-home-deadline-note className="text-xs/[18px] text-muted-foreground">{x.note.kind === 'InReview'
                      ? t(x.note.days === 1 ? 'home.deadline.inReviewOne' : 'home.deadline.inReview', { n: x.note.days })
                      : t(x.note.days === 1 ? 'home.deadline.baselineOne' : 'home.deadline.baseline', { n: x.note.days })}</span>}
                    <span className="text-xs/[18px] text-muted-foreground">{relative(x.date)}</span>
                    {/* Overdue never travels by colour alone (§13.0). */}
                    {(x.status || x.overdue) && <StatusPill status={x.overdue ? 'Overdue' : x.status} />}
                  </button>
                </li>
              ))}
            </ul>
          )}
          {!deadlinesOnly && d.upcomingDeadlines.truncated && <p className="border-t px-5 py-2.5 text-xs/[18px] text-muted-foreground">{t('home.truncated', { n: d.upcomingDeadlines.items.length })}</p>}
          {deadlinesOnly && d.upcomingDeadlines.page != null && <nav aria-label={t('home.deadlines')} className="flex flex-wrap items-center justify-end gap-3 border-t px-5 py-3 text-sm">
            <span className="mr-auto tabular-nums" aria-live="polite">{t('paging.range', { from: d.upcomingDeadlines.total === 0 ? 0 : (d.upcomingDeadlines.page - 1) * (d.upcomingDeadlines.pageSize ?? 100) + 1, to: Math.min(d.upcomingDeadlines.page * (d.upcomingDeadlines.pageSize ?? 100), d.upcomingDeadlines.total), total: d.upcomingDeadlines.total })}</span>
            <Button variant="outline" disabled={d.upcomingDeadlines.page <= 1} onClick={() => set('deadlinePage', String(d.upcomingDeadlines.page! - 1))}>{t('common.previous')}</Button>
            <span className="tabular-nums">{t('common.pageOf', { page: d.upcomingDeadlines.page, pages: Math.max(1, d.upcomingDeadlines.totalPages ?? 1) })}</span>
            <Button variant="outline" disabled={d.upcomingDeadlines.page >= (d.upcomingDeadlines.totalPages ?? 1)} onClick={() => set('deadlinePage', String(d.upcomingDeadlines.page! + 1))}>{t('common.next')}</Button>
          </nav>}
        </Section>
      )
      default: return null
    }
  }

  const layout = (d?.layout ?? []).filter(w => w.id !== 'weeklyCapacity' || (homeOverview && me.capabilities.workload))
  const shown = deadlinesOnly ? [{ id: 'upcomingDeadlines', hidden: false }] : editing ? layout : layout.filter((w) => !w.hidden)
  const welcome = t('sig.homeScope').split('{name}')
  return (
    <Page title={deadlinesOnly ? t('home.deadlines') : t('nav.home')} subtitle={<>{welcome.map((part, index) => <Fragment key={index}>{index > 0 && <PersonPanelTrigger userId={me.id} name={me.displayName} focusKey={`home:welcome:${index}`} />}{part}</Fragment>)}<PageHelp>{t('sig.homeHelp')}{d && <> {t('home.current', { time: fmtTime(d.asOf) })}</>}</PageHelp></>}
      titleBlock={d && !deadlinesOnly && <TitleBlock asOf={d.asOf} references={trends?.compareFrom ? [
        { label: t('home.trends.compareWith'), value: trends.compareFrom },
        ...(trends.compareThrough && trends.compareThrough !== trends.compareFrom ? [{ label: t('home.trends.latestComparison'), value: trends.compareThrough }] : []),
      ] : []} />} tabs={<WorkspaceTabs />} overlap={!!d && shown[0]?.id === 'metrics'}
      bandFooter={homeOverview && <div className="flex flex-wrap items-center gap-2 text-sm" aria-label={t('home.scopeFilters')}>
        <div className="flex items-center gap-2 rounded-lg border border-current/50 px-2 py-1"><span>{t('home.scopeProjects', { n: context?.projectsInScope ?? '—' })}</span><WorkspaceSelector displayLabel={scope.kind === 'all' ? presetProjectsLabel : undefined} contextNote={homePreset ? t('home.preset.additionalFilter') : undefined} /></div>
        {((preset?.available.length ?? 0) > 0 || homePreset) && <Popover open={presetOpen} onOpenChange={setPresetOpen}><PopoverTrigger asChild><button type="button" className="rounded-lg border border-current/50 px-3 py-2" data-home-preset>
          {t('home.preset.label')}: {homePreset ? textFor(HOME_PRESETS, homePreset) : t('home.preset.none')}
        </button></PopoverTrigger><PopoverContent className="space-y-3"><Field label={t('home.preset.label')} htmlFor="home-preset"><select id="home-preset" className={selectCls} value={homePreset ?? ''} disabled={presetSaving} onChange={e => { void choosePreset(e.target.value || null); setPresetOpen(false) }}>
          <option value="">{t('home.preset.none')}</option>{preset?.available.map(code => <option key={code} value={code}>{textFor(HOME_PRESETS, code)}</option>)}
          {homePreset && !preset?.available.includes(homePreset) && <option value={homePreset} disabled>{textFor(HOME_PRESETS, homePreset)}</option>}
        </select></Field>{homePreset && <Button variant="outline" size="sm" disabled={presetSaving} onClick={() => { void choosePreset(null); setPresetOpen(false) }}>{t('home.preset.clear')}</Button>}</PopoverContent></Popover>}
        <Popover open={statusFilterOpen} onOpenChange={setStatusFilterOpen}><PopoverTrigger asChild><button type="button" className="rounded-lg border border-current/50 px-3 py-2">{t('home.scopeStatus')}: {projectStatus ? projectStatus.split(',').map(tv).join(', ') : t('home.liveStatuses')}</button></PopoverTrigger>
          <PopoverContent><Field label={t('home.scopeStatus')} htmlFor="home-project-status"><select id="home-project-status" className={selectCls} value={projectStatus ?? ''} onChange={e => set('projectStatus', e.target.value || undefined)}>
            <option value="">{t('home.liveStatuses')}</option>{['Setup', 'Active', 'On Hold', 'Complete'].map(status => <option key={status} value={status}>{tv(status)}</option>)}
            {projectStatus?.includes(',') && <option value={projectStatus}>{projectStatus.split(',').map(tv).join(', ')}</option>}
          </select></Field></PopoverContent>
        </Popover>
        <Popover><PopoverTrigger asChild><button type="button" className="rounded-lg border border-current/50 px-3 py-2">{t('home.deadlines')}: {d?.from ?? from ?? '—'} – {d?.to ?? to ?? '—'}</button></PopoverTrigger>
          <PopoverContent className="space-y-3"><Field label={t('common.from')} htmlFor="home-scope-from"><Input id="home-scope-from" type="date" value={from ?? d?.from ?? ''} onChange={e => set('from', e.target.value || undefined)} /></Field><Field label={t('common.to')} htmlFor="home-scope-to"><Input id="home-scope-to" type="date" value={to ?? d?.to ?? ''} onChange={e => set('to', e.target.value || undefined)} /></Field><Button variant="outline" onClick={clearRange}>{t('filters.clear')}</Button></PopoverContent>
        </Popover>
        {preset?.code === 'DisciplineLead' && <button type="button" className="rounded-lg border border-current/50 px-3 py-2" onClick={() => setPresetOpen(true)} title={t('home.preset.disciplineHint')}>{t('home.preset.disciplines', { n: preset.disciplines })}</button>}
        {preset?.code === 'Supervisor' && <button type="button" className="rounded-lg border border-current/50 px-3 py-2" onClick={() => setPresetOpen(true)} title={t('home.preset.supervisorHint')}>{t('home.preset.people', { n: preset.people })}</button>}
        <button type="button" className="rounded-lg border border-current/50 px-3 py-2" onClick={() => setStatusFilterOpen(true)}>{t('home.addFilter')}</button>
        {deadlinesOnly && <Link className="underline underline-offset-4" to={scoped(drillScope, '/home', { projectStatus })}>{t('nav.home')}</Link>}
        {preset?.unownedMilestonesExcluded && <p className="basis-full text-xs">{t(preset.code === 'DisciplineLead' ? 'home.preset.disciplineHint' : 'home.preset.supervisorHint')}</p>}
      </div>} actions={d && !deadlinesOnly && <>{trends && <Button variant="outline" onClick={() => setComparing(true)}>{t('home.trends.details')}</Button>}{editing ? <>
      {!d.isDefault && <Button variant="outline" onClick={restore}><RotateCcw className="size-4" />{t('home.restore')}</Button>}
      <Button onClick={() => setEditing(false)}>{t('home.done')}</Button>
    </> : <Button variant="outline" onClick={() => setEditing(true)}><Pencil className="size-4" />{t('home.edit')}</Button>}</>}>
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      {homeOverview && homePreset && q.error && <Button variant="outline" disabled={presetSaving} onClick={() => void choosePreset(null)}>{t('home.preset.clear')}</Button>}
      {!visualSystem && !deadlinesOnly && <HomeTrendReferences trends={trends} />}
      {trends && <HomeTrendInspection trends={trends} dueDays={context?.dueWithinDays ?? 7} open={comparing} onClose={() => setComparing(false)} />}
      {!d ? !q.error && <Loading rows={8} /> : (
        <div className={homeOverview ? 'home-overview-layout' : 'space-y-6'} data-editing={homeOverview && editing || undefined}>
          {editing && <Notice icon={Pencil} title={t('home.edit')}>{t('home.editHint')}</Notice>}
          {shown.map((w) => {
            const i = layout.findIndex((x) => x.id === w.id)
            const name = runtimeText(`home.widget.${w.id}`)
            return (
              <div key={w.id} data-home-widget={homeOverview ? w.id : undefined} className={cn(editing && 'rounded-xl border border-dashed border-input p-3')}>
                {editing && (
                  <div className="mb-3 flex flex-wrap items-center gap-2 text-sm">
                    <span className="flex flex-1 items-center gap-2 font-semibold">{name}{w.hidden && <Chip tone="idle">{t('home.hidden')}</Chip>}</span>
                    <Button variant="outline" size="icon-sm" disabled={i === 0} aria-label={t('home.moveUp', { name })}
                      onClick={() => { const n = [...layout]; [n[i - 1], n[i]] = [n[i], n[i - 1]]; saveLayout(n) }}><ArrowUp className="size-4" /></Button>
                    <Button variant="outline" size="icon-sm" disabled={i === layout.length - 1} aria-label={t('home.moveDown', { name })}
                      onClick={() => { const n = [...layout]; [n[i], n[i + 1]] = [n[i + 1], n[i]]; saveLayout(n) }}><ArrowDown className="size-4" /></Button>
                    <Button variant="outline" size="sm" onClick={() => saveLayout(layout.map((x) => (x.id === w.id ? { ...x, hidden: !x.hidden } : x)))}>
                      {w.hidden ? <><Eye className="size-4" />{t('home.show')}</> : <><EyeOff className="size-4" />{t('home.hide')}</>}
                    </Button>
                  </div>
                )}
                <div className={cn(editing && w.hidden && 'opacity-50')}>{body(w)}</div>
              </div>
            )
          })}
          {!editing && shown.length === 0 && <div className="rounded-lg border bg-card"><Empty>{t('home.allHidden')}</Empty></div>}
        </div>
      )}
    </Page>
  )
}

/** Tasks by Project: one stacked bar per project, each lane segment opening that project's tasks in the lane. */
function ByProject({ d, scope, overview }: { d: HomeData; scope: Scope; overview: boolean }) {
  const [all, setAll] = useState(false)
  const rows = all ? d.tasksByProject : d.tasksByProject.slice(0, 8)
  const max = Math.max(1, ...d.tasksByProject.map((p) => p.total))
  const tones = overview ? { ...LANE_TONE, 'On Hold': 'var(--time-away-hatch, repeating-linear-gradient(135deg, #94a0b4 0 3px, #d5dae3 3px 6px))' } : LANE_TONE
  return (
    <Section id="h-projects" accent="mint" title={t('home.byProject')}>
      {rows.length === 0 ? <Empty>{t('home.noTasks')}</Empty> : (
        <ul className="space-y-2 px-5 pt-5">
          {rows.map((p) => (
            <li key={p.projectId} className="grid grid-cols-[minmax(0,1fr)_3rem] items-center gap-3 text-sm sm:grid-cols-[minmax(0,14rem)_1fr_3rem]">
              <span className="col-span-2 flex min-w-0 items-center gap-2 sm:col-span-1" title={`${p.projectNumber} ${p.name}`}>
                <IdentityDot id={p.projectId} colour={p.identityColour} /><span className="truncate"><Key>{p.projectNumber}</Key> {p.name}</span>
              </span>
              <span className="flex h-4 overflow-hidden rounded-full bg-secondary" style={{ width: `${(p.total / max) * 100}%` }}>
                {p.lanes.filter((l) => l.count > 0).map((l) => (
                  <Link data-home-lane={l.lane} key={l.lane} to={scoped(scope, '/tasks', { projectId: p.projectId, status: l.statuses })} className="block h-full hover:opacity-80"
                    style={{ width: `${(l.count / p.total) * 100}%`, background: tones[l.lane] }}
                    aria-label={t('home.projectSegment', { project: p.projectNumber, lane: laneName(l.lane), n: l.count })} title={`${laneName(l.lane)}: ${l.count}`} />
                ))}
              </span>
              <Link to={scoped(scope, '/tasks', { projectId: p.projectId, status: p.lanes.map((l) => l.statuses).join(',') })} className="text-right font-medium tabular-nums hover:underline">{p.total}</Link>
            </li>
          ))}
        </ul>
      )}
      {/* The colour key explains the bars, so it shows only when there are bars. */}
      {overview && rows.length > 0 && <div className="home-project-chart__axis" aria-hidden="true"><span><span>0</span>{max > 1 && <span>{Math.floor(max / 2)}</span>}<span>{max}</span></span></div>}
      {overview && rows.length > 0 ? <div className="home-project-chart__legend">
        {Object.entries(tones).map(([lane, tone]) => {
          const values = d.tasksByProject.flatMap(p => p.lanes.filter(l => l.lane === lane))
          const statuses = lane === 'On Hold' ? d.onHold.status : d.tasksByStatus.find(l => l.lane === lane)?.statuses
          return <Link key={lane} to={scoped(scope, '/tasks', { status: statuses })} aria-label={t('home.segment', { lane: laneName(lane), n: values.reduce((n, l) => n + l.count, 0) })}>
            <span className="home-chart-swatch" style={{ background: tone }} aria-hidden="true" />{laneName(lane)} <strong>{values.reduce((n, l) => n + l.count, 0)}</strong>
          </Link>
        })}
        {d.tasksByProject.length > 8 && <Button variant="link" size="sm" className="ml-auto" onClick={() => setAll(!all)}>{all ? t('home.fewer') : plural(d.tasksByProject.length, 'home.allOne', 'home.allMany', { n: d.tasksByProject.length })}</Button>}
      </div> : rows.length > 0 && <div className="flex flex-wrap items-center gap-x-4 gap-y-2 px-5 py-4 text-xs/[18px] text-muted-foreground">
        {Object.entries(LANE_TONE).map(([lane, c]) => <span key={lane} className="flex items-center gap-1.5"><span className="inline-block size-2.5 rounded-full" style={{ background: c }} aria-hidden />{laneName(lane)}</span>)}
        {d.tasksByProject.length > 8 && <Button variant="link" size="sm" className="ml-auto" onClick={() => setAll(!all)}>{all ? t('home.fewer') : plural(d.tasksByProject.length, 'home.allOne', 'home.allMany', { n: d.tasksByProject.length })}</Button>}
      </div>}
    </Section>
  )
}
