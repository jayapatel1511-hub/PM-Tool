import { PersonPanelTrigger } from '@/components/hub/person-panel-trigger'
import { ProjectAreaTabs } from '@/components/hub/workspace'
import { ProjectStatusNote } from '@/components/hub/project-status-note'
import { TitleBlock, formatReferenceTime } from '@/components/hub/title-block'
import { CountCard } from '@/components/hub/count-card'
import type { components } from '@/gen/api'
import { useQuery } from '@tanstack/react-query'
import { Monitor, Printer } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { Link, useSearchParams } from 'react-router'
import { AccentDot, ActiveFilters, DesktopOnly, Empty, ErrorBanner, Field, FilterBar, Missing, Notice, Page, PageHelp, SummaryTile, selectCls, tdCls, thCls } from '@/components/hub/common'
import { ExportMenu } from '@/components/hub/export'
import { Avatar, PeoplePicker } from '@/components/hub/people'
import { HealthPill, Key, StatusPill } from '@/components/hub/pills'
import { DataTable } from '@/components/hub/registers'
import type { Column } from '@/components/hub/table'
import { Why } from '@/components/hub/why'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { useReference } from '@/hooks/data'
import { useOrgPlacement } from '@/hooks/useOrgPlacement'
import { typedRequest } from '@/lib/api'
import { useFlag, useMe } from '@/lib/auth'
import { ago, fmtDate } from '@/lib/format'
import { plural, runtimeText, t, textFor, tv, type TextKey } from '@/lib/i18n'
import type { ProjectRow, ProjectStatusNoteData } from '@/lib/types'
import { cn } from '@/lib/utils'
import { ViewMenu } from '@/components/hub/views'
import { ProjectPlainProgress, ProjectStaffing } from './projects/project-list-details'
import type { SavedViewKeys } from '@/gen/registry.gen'
import './portfolio-review.css'

interface Row extends ProjectRow { statusNote?: ProjectStatusNoteData | null; projectType?: string; overdueDeliverables?: number; trend: { weekEnding: string; computed?: string | null; reported?: string | null }[] }
// The envelope and tiles are generated; the rows are the Projects list row (their source) with the members Portfolio adds.
type Portfolio = Omit<components['schemas']['PortfolioView'], 'projects'> & { projects: Row[] }

const FILTERS = ['q', 'pmId', 'leadUnitId', 'disciplineId', 'clientId', 'officeId', 'status', 'phaseId', 'health', 'computedHealth', 'projectTypeId', 'submissionWithinDays', 'mine', 'overdueDecisions'] as const satisfies readonly (SavedViewKeys['portfolio'] | 'page' | 'panel' | 'draftAccessReason')[]
const VISUAL_FILTERS = [...FILTERS, 'ids', 'starred', 'priority'] as const
const STATUSES = ['Setup', 'Active', 'On Hold', 'Complete']
const HEALTHS = ['Red', 'Yellow', 'Green', 'Grey'] as const
type Health = (typeof HEALTHS)[number]
const HEALTH_TEXT = { Red: 'health.Red', Yellow: 'health.Yellow', Green: 'health.Green', Grey: 'health.Grey' } as const satisfies Record<Health, TextKey>
const HEALTH_TILE_LABELS: Record<Exclude<Health, 'Grey'>, TextKey> = { Red: 'portfolio.red', Yellow: 'portfolio.yellow', Green: 'portfolio.green' }
const GROUP_OPTIONS = [
  { value: 'none', label: 'rcv.groupNone' },
  { value: 'client', label: 'rcv.groupClient' },
  { value: 'office', label: 'rcv.groupOffice' },
  { value: 'projectType', label: 'rcv.groupProjectType' },
] as const
/** Right-aligned figures. */
const COUNT = 'text-right tabular-nums'
/** Relative print widths: narrative facts have room to wrap, count columns stay compact. */
const PRINT_WEIGHTS: Record<string, number> = { project: 14.5, pm: 5.5, client: 5.5, office: 4.5, phase: 4, health: 5.5, staffing: 15.5,
  nextMilestone: 6.5, nextSubmission: 6.5, overdue: 4.5, blocked: 4.5, decisions: 5.5, issues: 4, deliverables: 6.5, attention: 5, trend: 4.5 }
const SWATCH: Record<string, string> = { Red: 'var(--bad)', Yellow: 'var(--warn)', Green: 'var(--ok)', Grey: 'var(--idle)' }
/** Health keeps its status symbol and colour (§13.0); the tiles' pastel accent only groups the three health counts. */
const HEALTH_MARK: Record<Exclude<Health, 'Grey'>, [string, string]> = { Red: ['■', 'text-bad'], Yellow: ['▲', 'text-warn'], Green: ['●', 'text-ok'] }
/** Portfolio Dashboard (§13.12, FR-PORT-01): which projects need help this week and why, computed and reported health side
 *  by side. Desktop and tablet only (§13.0). */
export function PortfolioPage() {
  const header = { title: t('nav.portfolio'), subtitle: <>{t('sig.pfScope')}<PageHelp>{t('sig.pfHelp')}</PageHelp></> }
  return (
    <DesktopOnly notice={<Page {...header} tabs={<ProjectAreaTabs />}><Notice icon={Monitor} title={t('app.phoneNotice')}
      action={<Button asChild variant="outline"><Link to="/my-work">{t('nav.myWork')}</Link></Button>}>{t('portfolio.phoneHint')}</Notice></Page>}>
      <Dashboard {...header} />
    </DesktopOnly>
  )
}

function Dashboard(header: { title: string; subtitle: ReactNode }) {
  const rollups = useFlag('status_rollups')
  const visualSystem = useFlag('visual_system')
  const projectScreens = useFlag('project_screens')
  const small = useFlag('small_ux')
  const me = useMe()
  const planner = me.capabilities.workload
  // FR-ADR-07: the Admin review strip is for Executives (and Admins), and only while flag.admin_review is on.
  const adminStrip = useFlag('admin_review') && (me.capabilities.admin || me.systemRoles.includes('Executive'))
  const ref = useReference()
  const org = useOrgPlacement([])
  const [sp, setSp] = useSearchParams()
  const set = (k: string, v?: string | null) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); setSp(n, { replace: true }) }
  const group = rollups ? sp.get('group') ?? 'none' : 'none'
  const filterKeys = visualSystem ? VISUAL_FILTERS : FILTERS
  const filters = Object.fromEntries(filterKeys.map((k) => [k, sp.get(k)]))
  const q = useQuery({ queryKey: ['portfolio', filters], queryFn: async () => await typedRequest('get', '/api/v1/portfolio', { query: filters }) as Portfolio })
  const refSelect = (key: string, label: string, items: { id: string; name: string; isActive?: boolean }[] | undefined, any: string) => (
    <Field label={label} htmlFor={`pf-${key}`} className="w-full sm:w-44">
      <select id={`pf-${key}`} className={selectCls} value={filters[key] ?? ''} onChange={(e) => set(key, e.target.value)}>
        <option value="">{any}</option>{items?.filter((x) => x.isActive !== false).map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
      </select>
    </Field>
  )
  const name = (items: { id: string; name: string }[] | undefined, id: string | null) => items?.find((x) => x.id === id)?.name
  const pmName = q.data?.projects.find((p) => p.pm.id === filters.pmId)?.pm.displayName

  // Active filters as removable tokens (§13.0 Filters); Clear empties the URL as before.
  const tokens: [string, string, ReactNode?][] = ([
    ['q', t('common.search'), filters.q],
    ['pmId', t('portfolio.pm'), pmName ?? t('portfolio.selectedPm')],
    ['leadUnitId', t('org.leadUnit'), org.units.find(u => u.id === filters.leadUnitId)?.name],
    ['disciplineId', t('common.discipline'), name(ref.data?.disciplines, filters.disciplineId)],
    ['clientId', t('portfolio.client'), name(ref.data?.clients, filters.clientId)],
    ['officeId', t('admin.office'), name(ref.data?.offices, filters.officeId)],
    ['phaseId', t('field.PhaseId'), name(ref.data?.phases, filters.phaseId)],
    ['projectTypeId', t('projects.type'), name(ref.data?.projectTypes, filters.projectTypeId)],
    ['status', t('common.status'), filters.status && tv(filters.status)],
    ['health', t('portfolio.health'), filters.health && runtimeText(`health.${filters.health}`)],
    ['computedHealth', t('projects.filter.computedHealth'), filters.computedHealth?.split(',').map((h) => runtimeText(`health.${h}`)).join(', ')],
    ['submissionWithinDays', t('portfolio.within'), filters.submissionWithinDays],
    ['overdueDecisions', t('portfolio.decisions'), t('common.yes')],
    ['mine', t('portfolio.mine'), filters.mine === 'false' ? t('common.no') : filters.mine],
    ...(visualSystem ? [
      ['ids', t('projects.filter.chosen'), filters.ids?.split(',').filter(Boolean).length],
      ['starred', t('projects.star'), filters.starred === 'true' ? t('common.yes') : filters.starred],
      ['priority', t('projects.col.priority'), filters.priority],
    ] : []),
  ] as [string, string, ReactNode?][]).filter(([k]) => !!filters[k])
  const clear = () => setSp(new URLSearchParams(), { replace: true })
  const healthLabel = (h: Exclude<Health, 'Grey'>, key: TextKey) => (
    <span className="inline-flex items-center gap-1.5"><span aria-hidden className={HEALTH_MARK[h][1]}>{HEALTH_MARK[h][0]}</span>{t(key)}</span>
  )
  const tiles = q.data?.tiles
  const destination = (key: string, value: string) => { const n = new URLSearchParams(sp); n.set(key, value); return `?${n}` }
  // A tighter existing date filter stays in force, so this card never widens its
  // counted cohort while navigating to the within-fourteen-days intersection.
  const existingWindow = Number(filters.submissionWithinDays)
  const submissionWindow = Number.isInteger(existingWindow) && existingWindow > 0 ? Math.min(existingWindow, 14) : 14
  const where = (key: string) => {
    const facts = q.data?.cardFacts?.[key]
    return facts ? t('portfolioScreens.where', {
      projects: plural(facts.projects, 'scope.oneProject', 'scope.nProjects'),
      clients: plural(facts.clients, 'scope.oneClient', 'scope.nClients'),
    }) : t('portfolioScreens.whereUnavailable')
  }
  // AC-CNT-01: a health tile counts Active projects by computed health, so it opens exactly those rows.
  const healthLink = (h: string) => { const n = new URLSearchParams(sp); n.set('computedHealth', h); n.set('status', 'Active'); return `?${n}` }
  const rowLinks = (r: Row) => {
    const base = `/projects/${encodeURIComponent(r.projectNumber)}`
    return { overdue: `${base}/tasks?overdue=true`, blocked: `${base}/tasks?blocked=true`, decisions: `${base}/decisions?indicator=overdue`,
      issues: `${base}/issues?indicator=open&severity=High`, critical: `${base}/dashboard?severity=Critical#attention`, warning: `${base}/dashboard?severity=Warning#attention`, deliverables: `${base}/deliverables?indicator=overdue` }
  }
  const groupKey = (r: Row) => group === 'client' ? r.client.id : group === 'office' ? r.officeId ?? '' : group === 'projectType' ? r.projectTypeId ?? '' : ''
  const groupName = (r: Row) => group === 'client' ? r.client.name : group === 'office' ? r.office ?? t('rcv.notSet') : r.projectType ?? name(ref.data?.projectTypes, r.projectTypeId ?? null) ?? t('rcv.notSet')
  const groups = new Map<string, Row[]>()
  for (const r of q.data?.projects ?? []) { const key = groupKey(r); groups.set(key, [...(groups.get(key) ?? []), r]) }
  const grouped = [...groups.entries()].sort(([a, ar], [b, br]) => !a ? 1 : !b ? -1 : groupName(ar[0]).localeCompare(groupName(br[0])))
  const ordered = group === 'none' ? q.data?.projects ?? [] : grouped.flatMap(([, g]) => g)
  const groupLink = (key: string, health?: string) => `?${new URLSearchParams({ ...Object.fromEntries(sp), [group === 'client' ? 'clientId' : group === 'office' ? 'officeId' : 'projectTypeId']: key, ...(health ? { computedHealth: health } : {}) })}`
  const attention = (r: Row) => <>
    <Link to={rowLinks(r).critical} className={cn(r.attentionCritical > 0 && 'font-semibold text-bad')}><span aria-hidden>■</span><span className="sr-only">{tv('Critical')}</span> {r.attentionCritical}</Link>
    <Link to={rowLinks(r).warning} className={cn('ml-3', r.attentionWarning > 0 && 'font-semibold text-warn')}><span aria-hidden>▲</span><span className="sr-only">{tv('Warning')}</span> {r.attentionWarning}</Link>
  </>
  // The columns, their order and header text are the list's own: every one is fixed and none sorts (FR-REC-14 adds no surface).
  const columns: Column<Row>[] = [
    { id: 'project', label: t('portfolio.project'), className: 'min-w-60', cell: (r) => <>
      {projectScreens && <details className="mb-2"><summary>{t('projectScreens.progressFacts')}</summary><div className="pt-2"><ProjectPlainProgress row={r} /></div></details>}
      {r.statusNote && <details className="mb-2"><summary>{t('statusNote.title')}</summary><ProjectStatusNote note={r.statusNote} /></details>}
      <div className="flex items-start gap-2">
        <AccentDot id={r.id} className="mt-1.5" />
        <div className="min-w-0">
          <Link to={`/projects/${encodeURIComponent(r.projectNumber)}/dashboard`} className="hover:underline"><Key>{r.projectNumber}</Key> <span className="font-semibold">{r.name}</span></Link>
          {r.status !== 'Active' && <div className="mt-1"><StatusPill status={r.status} /></div>}
        </div>
      </div>
      {projectScreens && <div className="portfolio-print-only"><ProjectPlainProgress row={r} /></div>}
    </> },
    { id: 'pm', label: t('portfolio.pm'), cell: (r) => <span className="flex items-center gap-2 whitespace-nowrap"><Avatar id={r.pm.id} name={r.pm.displayName} /><PersonPanelTrigger userId={r.pm.id} name={r.pm.displayName} focusKey={`portfolio:project:${r.id}:pm:screen`} /></span> },
    { id: 'client', label: t('portfolio.client'), cell: (r) => r.client.name },
    { id: 'office', label: t('admin.office'), className: 'whitespace-nowrap', cell: (r) => r.office || <Missing /> },
    { id: 'phase', label: t('field.PhaseId'), className: 'whitespace-nowrap', cell: (r) => r.phase || <Missing /> },
    { id: 'health', label: t('portfolio.health'), className: 'min-w-44', cell: (r) => <Health r={r} /> },
    ...(projectScreens ? [{ id: 'staffing', label: t('projectScreens.staffing'), cell: (r: Row) => <ProjectStaffing row={r} /> }] : []),
    { id: 'nextMilestone', label: t('portfolio.nextMilestone'), className: 'min-w-40', cell: (r) => r.nextMilestone ? <><div>{r.nextMilestone.name}</div>
      <div className="mt-0.5 flex flex-wrap items-center gap-1.5 text-xs/[18px] text-muted-foreground tabular-nums">{fmtDate(r.nextMilestone.date)} {r.nextMilestone.status && <StatusPill status={r.nextMilestone.status} />}</div></> : <Missing /> },
    { id: 'nextSubmission', label: t('portfolio.nextSubmission'), className: 'min-w-36', cell: (r) => r.nextSubmission ? <><div>{r.nextSubmission.name}</div><div className="mt-0.5 text-xs/[18px] text-muted-foreground tabular-nums">{fmtDate(r.nextSubmission.date)}</div></> : <Missing /> },
    { id: 'overdue', label: t('portfolio.overdue'), className: COUNT, cell: (r) => <Count n={r.overdueTasks} to={rowLinks(r).overdue} bad /> },
    { id: 'blocked', label: t('portfolio.blocked'), className: COUNT, cell: (r) => <Count n={r.blockedTasks} to={rowLinks(r).blocked} bad /> },
    { id: 'decisions', label: t('portfolio.decisionsOverdue'), className: COUNT, cell: (r) => <Count n={r.overdueDecisions} to={rowLinks(r).decisions} bad /> },
    { id: 'issues', label: t('portfolio.highIssues'), className: COUNT, cell: (r) => <Count n={r.highIssues} to={rowLinks(r).issues} /> },
    ...(rollups ? [{ id: 'deliverables', label: t('rcv.overdueDeliverables'), className: COUNT, cell: (r: Row) => <Count n={r.overdueDeliverables ?? 0} to={rowLinks(r).deliverables} bad /> }] : []),
    { id: 'attention', label: t('portfolio.attention'), className: cn(COUNT, 'whitespace-nowrap'), cell: attention },
    { id: 'trend', label: t('portfolio.trend'), cell: (r) => <Trend points={r.trend} /> },
    { id: 'coordination', label: t('common.actions'), className: 'whitespace-nowrap',
      cell: (r) => <Link className="text-primary underline underline-offset-4 hover:text-foreground" to={`/projects/${encodeURIComponent(r.projectNumber)}/coordination`}>{t('portfolio.coordination')}</Link> },
  ]
  const table = {
    visible: columns,
    header: (c: Column<Row>) => <th key={c.id} scope="col" className={cn(thCls, c.className?.includes('text-right') && 'text-right')}>{c.id === 'coordination' ? <span className="sr-only">{c.label}</span> : c.label}</th>,
    cell: (c: Column<Row>, r: Row) => <td key={c.id} className={cn(tdCls, c.className)}>{c.cell(r)}</td>,
  }
  const subtotal = (rows: Row[], column: 'overdueTasks' | 'blockedTasks' | 'overdueDecisions' | 'highIssues' | 'overdueDeliverables') => rows.reduce((sum, r) => sum + (r[column] ?? 0), 0)
  const renderGroupRow = (key: string, activeColumns = columns) => {
    const rows = groups.get(key) ?? []
    if (projectScreens) {
      const countStart = activeColumns.findIndex(c => c.id === 'overdue')
      const countFields = { overdue: 'overdueTasks', blocked: 'blockedTasks', decisions: 'overdueDecisions', issues: 'highIssues', deliverables: 'overdueDeliverables' } as const
      return <tr className="border-t bg-muted" data-portfolio-group={key}>
        <th scope="rowgroup" colSpan={countStart} className={thCls}>{groupName(rows[0])} · {key ? <Link to={groupLink(key)} className="underline underline-offset-2">{rows.length} {t('nav.projects')}</Link> : <span title={t('rcv.notSetDefinition')}>{rows.length} {t('nav.projects')}</span>}
          <span className="ml-3 inline-flex flex-wrap gap-3">{(['Red', 'Yellow', 'Green'] as const).map(health => <span key={health}>{key ? <Link to={groupLink(key, health)}>{t(`health.${health}`)} {rows.filter(r => r.computedHealth === health).length}</Link> : <span>{t(`health.${health}`)} {rows.filter(r => r.computedHealth === health).length}</span>}</span>)}</span>
        </th>
        {activeColumns.slice(countStart).map(c => {
          const field = countFields[c.id as keyof typeof countFields]
          return <td key={c.id} className={cn(tdCls, COUNT)}>{field ? key ? <Link to={groupLink(key)} title={t('rcv.subtotalDefinition')}>{subtotal(rows, field)}</Link> : subtotal(rows, field)
            : c.id === 'attention' ? <span>■ {rows.reduce((sum, r) => sum + r.attentionCritical, 0)} · ▲ {rows.reduce((sum, r) => sum + r.attentionWarning, 0)}</span> : null}</td>
        })}
      </tr>
    }
    return <tr className="border-t bg-muted" data-portfolio-group={key}><th scope="rowgroup" colSpan={8} className={thCls}>
      {groupName(rows[0])} · {key ? <Link to={groupLink(key)} className="underline underline-offset-2">{rows.length} {t('nav.projects')}</Link> : <span title={t('rcv.notSetDefinition')}>{rows.length} {t('nav.projects')}</span>}
      <span className="ml-3 inline-flex gap-3">{(['Red', 'Yellow', 'Green'] as const).map((health) => <span key={health}>{key ? <Link to={groupLink(key, health)}>{t(`health.${health}`)} {rows.filter(r => r.computedHealth === health).length}</Link> : <span title={t('rcv.notSetDefinition')}>{t(`health.${health}`)} {rows.filter(r => r.computedHealth === health).length}</span>}</span>)}</span>
    </th>{(['overdueTasks', 'blockedTasks', 'overdueDecisions', 'highIssues', 'overdueDeliverables'] as const).map((column) => <td key={column} className={cn(tdCls, COUNT)}>{key ? <Link to={groupLink(key)} aria-label={`${subtotal(rows, column)} · ${t('rcv.subtotalDefinition')}`} title={t('rcv.subtotalDefinition')} className="underline underline-offset-2">{subtotal(rows, column)}</Link> : <span title={t('rcv.notSetDefinition')}>{subtotal(rows, column)}</span>}</td>)}
    <td className={cn(tdCls, COUNT)}><span title={t('rcv.subtotalDefinition')}>■ {rows.reduce((sum,r)=>sum+r.attentionCritical,0)} · ▲ {rows.reduce((sum,r)=>sum+r.attentionWarning,0)}</span></td><td colSpan={2} /></tr>
  }
  const groupRow = (key: string) => renderGroupRow(key)
  const printColumns = columns.filter(c => c.id !== 'coordination').map(column => column.id === 'pm' ? { ...column, cell: (r: Row) => <span className="flex items-center gap-2 whitespace-nowrap"><Avatar id={r.pm.id} name={r.pm.displayName} />{r.pm.displayName}</span> } : column)
  const printDetail = (row: Row) => {
    const healthNote = row.reportedHealth !== row.computedHealth && row.healthOverrideNote
    if (!row.statusNote && !healthNote) return null
    return <div className="space-y-2">
      {row.statusNote && <ProjectStatusNote note={row.statusNote} />}
      {healthNote && <section data-portfolio-health-note>
        <p className="font-medium">{t('health.computed')}: {textFor(HEALTH_TEXT, row.computedHealth)} · {t('health.reported')}: {textFor(HEALTH_TEXT, row.reportedHealth)}</p>
        <p>“{row.healthOverrideNote}” — {t('portfolio.noteBy', { name: row.overrideBy ?? '', when: ago(row.healthOverrideAt) })}</p>
      </section>}
    </div>
  }
  const printWeight = printColumns.reduce((sum, column) => sum + (PRINT_WEIGHTS[column.id] ?? 4), 0)
  const printTable = {
    visible: printColumns,
    header: (column: Column<Row>) => <th key={column.id} scope="col" data-portfolio-column={column.id} className={cn(thCls, column.className?.includes('text-right') && 'text-right')}
      style={{ width: `${(PRINT_WEIGHTS[column.id] ?? 4) / printWeight * 100}%` }}>{column.label}</th>,
    cell: (column: Column<Row>, row: Row) => <td key={column.id} data-portfolio-column={column.id} className={cn(tdCls, column.className)}>{column.id === 'health' ? <PrintHealth r={row} /> : column.cell(row)}</td>,
  }
  const printScope = [q.data?.ownOnly ? t('portfolio.mine') : t('projects.all'), ...tokens.map(([, label, value]) => `${label}: ${typeof value === 'string' || typeof value === 'number' ? value : t('common.unavailable')}`)].join(' · ')
  return (
    <Page {...header} className={projectScreens ? 'portfolio-review' : undefined} overlap={visualSystem} tabs={<ProjectAreaTabs />} titleBlock={q.data && <TitleBlock asOf={q.data.asOf} />} actions={<>{(small || projectScreens) && planner && <Button asChild variant="outline"><Link to="/planner?groupBy=homeDiscipline">{t('interaction.capacityByDiscipline')}</Link></Button>}{projectScreens && <Button variant="outline" disabled={!q.data || q.isFetching || !!q.error} onClick={() => window.print()}><Printer className="size-4" />{t('portfolioScreens.print')}</Button>}<ViewMenu listType="portfolio" /><ExportMenu path="portfolio/export" params={filters} name="portfolio" /></>}>
      {projectScreens && !visualSystem && <div className="portfolio-print-only portfolio-print-band"><h1>{t('nav.portfolio')}</h1><dl aria-label={t('visual.currentValues')}><dt>{t('visual.currentValues')}</dt><dd>{q.data?.asOf ? <time dateTime={q.data.asOf}>{formatReferenceTime(q.data.asOf, me.settings.orgTimeZone) ?? t('common.unavailable')}</time> : t('common.unavailable')}</dd></dl></div>}
      {tiles && (
        visualSystem ? <div className="home-count-cards portfolio-count-cards">
          <CountCard label={t('portfolio.active')} value={tiles.active} tone="blue" definition={t('sig.def.pfActive')} where={where('active')} to={destination('status', 'Active')} />
          {(['Red', 'Yellow', 'Green'] as const).map(h => <CountCard key={h} label={healthLabel(h, HEALTH_TILE_LABELS[h])} value={tiles[h.toLowerCase() as 'red']} tone={h === 'Red' ? 'red' : h === 'Yellow' ? 'amber' : 'green'} definition={t('sig.def.pfHealth', { health: t(`health.${h}`) })} where={where(h.toLowerCase())} to={healthLink(h)} />)}
          <CountCard label={t('portfolio.submissions')} value={tiles.submissions14} tone="amber" definition={t('sig.def.pfSubmissions')} where={where('submissions14')} to={destination('submissionWithinDays', String(submissionWindow))} />
          <CountCard label={t('portfolio.decisions')} value={tiles.overdueDecisions} tone="red" definition={t('sig.def.pfDecisions')} where={where('overdueDecisions')} to={destination('overdueDecisions', 'true')} />
        </div> : <div className={cn('grid grid-cols-2 gap-3 sm:grid-cols-3 sm:gap-4 xl:grid-cols-6', projectScreens && 'portfolio-print-legacy-tiles')}>
          <SummaryTile label={t('portfolio.active')} value={tiles.active} accent="blue" hint={t('sig.def.pfActive')} onClick={() => set('status', 'Active')} />
          {(['Red', 'Yellow', 'Green'] as const).map((h) => (
            <SummaryTile key={h} label={healthLabel(h, HEALTH_TILE_LABELS[h])} value={tiles[h.toLowerCase() as 'red']} accent="lavender" hint={t('sig.def.pfHealth', { health: runtimeText(`health.${h}`) })} to={healthLink(h)} />
          ))}
          <SummaryTile label={t('portfolio.submissions')} value={tiles.submissions14} accent="mint" hint={t('sig.def.pfSubmissions')} onClick={() => set('submissionWithinDays', '14')} />
          <SummaryTile label={t('portfolio.decisions')} value={tiles.overdueDecisions} accent="peach" hint={t('sig.def.pfDecisions')} onClick={() => set('overdueDecisions', 'true')} />
        </div>
      )}
      {adminStrip && <div className={projectScreens ? 'no-print' : undefined}><AdminReviewStrip /></div>}
      {rollups && <Field label={t('rcv.group')} htmlFor="pf-group" className={cn('w-48', projectScreens && 'no-print')}><select id="pf-group" className={selectCls} value={group} onChange={(e) => set('group', e.target.value === 'none' ? null : e.target.value)}>
        {GROUP_OPTIONS.map(({ value, label }) => <option key={value} value={value}>{t(label)}</option>)}
      </select></Field>}
      <div className={projectScreens ? 'no-print' : undefined}><FilterBar activeCount={tokens.length}
        search={<Field label={t('common.search')} htmlFor="pf-q">
          <Input id="pf-q" key={filters.q ? 'q' : 'empty'} type="search" placeholder={t('common.search')} defaultValue={filters.q ?? ''} onChange={(e) => set('q', e.target.value)} />
        </Field>}
        active={<ActiveFilters tokens={tokens.map(([key, label, value]) => ({ key, label, value: value ?? t('common.dash') }))} onRemove={(k) => set(k, null)} onClear={clear} />}>
        <div className="flex flex-wrap items-end gap-3">
          <Field label={t('portfolio.pm')} htmlFor="pf-pm" className="w-full sm:w-52">
            <PeoplePicker id="pf-pm" value={filters.pmId} valueName={pmName} onChange={(v) => set('pmId', v)} placeholder={t('portfolio.anyPm')} label={t('portfolio.pm')} />
          </Field>
          {org.enabled && <Field label={t('org.leadUnit')} htmlFor="pf-lead-unit" className="w-full sm:w-52"><select id="pf-lead-unit" className={selectCls} value={filters.leadUnitId ?? ''} onChange={(e) => set('leadUnitId', e.target.value)}><option value="">{t('org.anyUnit')}</option><option value="00000000-0000-0000-0000-000000000000">{t('organisation.noUnit')}</option>{org.units.map(u => <option key={u.id} value={u.id}>{u.name}</option>)}</select></Field>}
          {refSelect('disciplineId', t('common.discipline'), ref.data?.disciplines, t('projects.anyDiscipline'))}
          {refSelect('clientId', t('portfolio.client'), ref.data?.clients, t('portfolio.anyClient'))}
          {refSelect('officeId', t('admin.office'), ref.data?.offices, t('reports.anyOffice'))}
          {refSelect('phaseId', t('field.PhaseId'), ref.data?.phases, t('portfolio.anyPhase'))}
          {refSelect('projectTypeId', t('projects.type'), ref.data?.projectTypes, t('portfolio.anyType'))}
          <Field label={t('common.status')} htmlFor="pf-status" className="w-full sm:w-48">
            <select id="pf-status" className={selectCls} value={filters.status ?? ''} onChange={(e) => set('status', e.target.value)}>
              <option value="">{t('portfolio.liveStatuses')}</option>{STATUSES.map((s) => <option key={s} value={s}>{tv(s)}</option>)}
            </select>
          </Field>
          <Field label={t('portfolio.health')} htmlFor="pf-health" className="w-full sm:w-40">
            <select id="pf-health" className={selectCls} value={filters.health ?? ''} onChange={(e) => set('health', e.target.value)}>
              <option value="">{t('portfolio.anyHealth')}</option>{HEALTHS.map((h) => <option key={h} value={h}>{t(`health.${h}`)}</option>)}
            </select>
          </Field>
          <Field label={t('portfolio.within')} htmlFor="pf-within" className="w-full sm:w-48">
            <Input id="pf-within" type="number" min={1} className="tabular-nums" value={filters.submissionWithinDays ?? ''} onChange={(e) => set('submissionWithinDays', e.target.value)} />
          </Field>
          {q.data && (q.data.ownOnly || filters.mine) && (
            <label className="flex min-h-(--control-h) items-center gap-2 text-sm"><Checkbox checked={q.data.ownOnly} onCheckedChange={(c) => set('mine', c ? null : 'false')} />{t('portfolio.mine')}</label>
          )}
        </div>
      </FilterBar></div>
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      {projectScreens && q.data?.changes && <PortfolioMoveLine data={q.data.changes} />}
      {(q.isPending || q.data) && <div className="portfolio-screen-table"><DataTable table={table} rows={ordered} loading={q.isPending} caption={t('nav.portfolio')} current={() => false} cards={false} oneBody
        groupBy={group === 'none' ? undefined : groupKey} groupRow={groupRow}
        empty={<Empty action={tokens.length > 0 && <Button variant="outline" onClick={clear}>{t('filters.clear')}</Button>}>{t('portfolio.empty')}</Empty>} /></div>}
      {projectScreens && q.data && <div className="portfolio-print-only portfolio-print-table">
        <p className="portfolio-print-scope">{t('portfolioScreens.printScope', { scope: printScope, group: t(GROUP_OPTIONS.find(option => option.value === group)?.label ?? 'rcv.groupNone') })}</p>
        <DataTable table={printTable} rows={ordered} loading={false} caption={t('nav.portfolio')} current={() => false} cards={false} oneBody wide
          groupBy={group === 'none' ? undefined : groupKey} groupRow={key => renderGroupRow(key, printColumns)}
          detail={printDetail} empty={<p>{t('portfolio.empty')}</p>} />
      </div>}
    </Page>
  )
}

/** Exact scoped positive project lists stay inline; no derived-ID URL can truncate them. */
function PortfolioMoveLine({ data }: { data: NonNullable<Portfolio['changes']> }) {
  const [opened, setOpened] = useState<'health' | 'milestones' | 'status' | null>(null)
  const fact = (key: 'health' | 'milestones' | 'status') => {
    const metric = data[key], n = metric.count ?? metric.observed
    if (metric.count == null && metric.observed === 0) return t(`portfolioScreens.${key}Unavailable`)
    const text = t(`portfolioScreens.${key}${n === 1 ? 'One' : 'Many'}`, { n })
    return metric.count == null ? t('portfolioScreens.atLeast', { fact: text }) : text
  }
  return <section className="portfolio-change-line no-print space-y-2 text-sm" aria-label={t('portfolioScreens.moved', { n: data.days })}>
    <p className="flex flex-wrap items-baseline gap-x-2 gap-y-1"><span>{t('portfolioScreens.moved', { n: data.days })}</span>{(['health', 'milestones', 'status'] as const).map(key => <button key={key} type="button" className="text-left underline underline-offset-4" aria-expanded={opened === key} aria-controls={opened === key ? `portfolio-moves-${key}` : undefined} onClick={() => setOpened(opened === key ? null : key)}>{fact(key)}</button>)}</p>
    <p className="text-xs text-muted-foreground">{t('portfolioScreens.scope', { n: data.scopeProjects })} · {t('portfolioScreens.window', { from: fmtDate(data.fromDate) })}</p>
    {data.scopeCeilingReached && <p>{t('portfolioScreens.ceiling')}</p>}
    {(['health', 'milestones', 'status'] as const).some(key => data[key].count == null) && <p>{t('portfolioScreens.historyUnavailable')}</p>}
    {opened && <div id={`portfolio-moves-${opened}`} className="space-y-2 rounded border p-4" role="region" aria-label={fact(opened)}>
      <h3 className="font-medium">{fact(opened)}</h3><p>{t(`portfolioScreens.${opened}Definition`)}</p>
      {data[opened].unavailableProjects > 0 && <p>{t('portfolioScreens.coverage', { n: data[opened].unavailableProjects })}</p>}
      {data[opened].projects.length ? <ul className="max-h-80 space-y-1 overflow-y-auto">{data[opened].projects.map(project => <li key={project.id}><Link className="underline underline-offset-2" to={`/projects/${encodeURIComponent(project.projectNumber)}/dashboard`}><Key>{project.projectNumber}</Key> {project.name}</Link></li>)}</ul> : <p>{t('portfolioScreens.noConfirmed')}</p>}
    </div>}
  </section>
}

/** FR-ADR-07: the last recorded Admin review's date and counts, nothing else; it never opens a list. */
function AdminReviewStrip() {
  const q = useQuery({ queryKey: ['portfolio', 'admin-review'], queryFn: () => typedRequest('get', '/api/v1/portfolio/admin-review', {}) })
  if (!q.data) return null
  return (
    <section aria-label={t('portfolio.adminReview')} data-admin-review-strip className="flex flex-wrap items-baseline gap-x-5 gap-y-1 rounded-lg border bg-card px-4 py-2.5 text-sm">
      <h2 className="font-semibold">{t('portfolio.adminReview')}</h2>
      {q.data.reviewedAt
        ? <><span className="text-muted-foreground">{t('portfolio.adminReviewLast', { date: fmtDate(q.data.reviewedAt) })}</span>
          {q.data.checks.map((c) => <span key={c.key}>{c.title} <span className="font-semibold tabular-nums">{c.count}</span></span>)}</>
        : <span className="text-muted-foreground">{t('portfolio.adminReviewNone')}</span>}
    </section>
  )
}

/** A count column: the figure links to its list; a non-zero overdue or blocked count is emphasised (its header names it). */
function Count({ n, bad, to }: { n: number; bad?: boolean; to: string }) {
  return <Link to={to} className={cn('hover:underline', bad && n > 0 && 'font-semibold text-bad')}>{n}</Link>
}

/** Print labels use their own line so each symbol and short health value stays together. */
function PrintHealth({ r }: { r: Row }) {
  const differs = r.reportedHealth !== r.computedHealth
  return <div className="portfolio-print-health space-y-1">
    {differs ? <>
      <div><span className="block">{t('health.computed')}</span><HealthPill health={r.computedHealth} /></div>
      <div><span className="block">{t('health.reported')}</span><HealthPill health={r.reportedHealth} /></div>
    </> : <HealthPill health={r.reportedHealth} />}
  </div>
}

/** §16.4: when they differ, both values are shown, with the PM's note and its age. */
function Health({ r }: { r: Row }) {
  const differs = r.reportedHealth !== r.computedHealth
  return (
    <div className="space-y-1">
      <Why reasons={r.healthReasons} title={`${r.projectNumber} · ${t('health.whyTitle', { health: runtimeText(`health.${r.computedHealth}`) })}`}>
        {differs ? <span className="flex flex-wrap gap-1"><HealthPill health={r.computedHealth} label={t('health.computed')} /><HealthPill health={r.reportedHealth} label={t('health.reported')} /></span>
          : <HealthPill health={r.reportedHealth} />}
      </Why>
      {differs && r.healthOverrideNote && <p className="max-w-56 text-xs/[18px] text-muted-foreground">“{r.healthOverrideNote}” — {t('portfolio.noteBy', { name: r.overrideBy ?? '', when: ago(r.healthOverrideAt) })}</p>}
    </div>
  )
}

/** FR-HLT-03: eight weeks of computed health, oldest first. */
function Trend({ points }: { points: Row['trend'] }) {
  const label = points.map((p) => `${fmtDate(p.weekEnding)}: ${p.computed ? runtimeText(`health.${p.computed}`) : t('portfolio.noSnapshot')}`).join('; ')
  return (
    <span className="flex gap-0.5" role="img" aria-label={t('portfolio.trendLabel', { list: label })} title={label}>
      {points.map((p) => <span key={p.weekEnding} className={cn('inline-block h-4 w-2 rounded-[2px]', !p.computed && 'border border-dashed border-input')} style={p.computed ? { background: SWATCH[p.computed] } : undefined} />)}
    </span>
  )
}
