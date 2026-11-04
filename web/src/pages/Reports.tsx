import { useQuery } from '@tanstack/react-query'
import { ArrowLeft, ChevronsUpDown, ListFilter } from 'lucide-react'
import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { Link, useLocation, useParams, useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { Empty, ErrorBanner, Field, Loading, Missing, Page, PageHelp, TableRegion, selectCls, tdCls, thCls, useIsPhone } from '@/components/hub/common'
import { TitleBlock } from '@/components/hub/title-block'
import { PersonPanelTrigger } from '@/components/hub/person-panel-trigger'
import { reportUserReference } from '@/components/hub/report-user-reference'
import { useFlag } from '@/lib/auth-context'
import { ExportMenu } from '@/components/hub/export'
import { ViewMenu } from '@/components/hub/views'
import { useItemPanel } from '@/components/hub/panel-host'
import { PeoplePicker } from '@/components/hub/people'
import { Key, StatusPill } from '@/components/hub/pills'
import { itemHref } from '@/components/hub/search'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { useReference } from '@/hooks/data'
import type { components } from '@/gen/api'
import { typedRequest } from '@/lib/api'
import { ChangeList, type ActivityRow } from '@/components/hub/activity'
import { addDays, fmtDate, fmtTime, today } from '@/lib/format'
import { plural, t, tv , runtimeText } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { clearReportInputState, ReportTaskInputs } from './report-task-inputs'
import { captureRecordContextOrigin } from '@/lib/record-context-navigation'
import { ReportCapacityComparison, reportChartFocusParameters } from './report-capacity-comparison'
import './reports-catalogue.css'

type Schemas = components['schemas']
type Param = Schemas['ReportParamView']
type Column = Schemas['ReportColumn']
/** The rows differ by report and are read through `columns`, so the contract leaves them free-form. */
type ReportRun = Omit<Schemas['ReportView'], 'rows'> & { rows: Record<string, any>[] }

const useCatalogue = () => {
  const enhanced = useFlag('capacity_screens')
  return useQuery({ queryKey: enhanced ? ['reports', 'capacity_screens'] : ['reports'], queryFn: () => typedRequest('get', '/api/v1/reports', {}), staleTime: 5 * 60_000 })
}
const card = 'rounded-lg border bg-card'

/** Reports catalogue (§13.18, §19, §41 R5): deterministic reports, one table row each with its description; the title
 *  opens the report. */
export function ReportsPage() {
  const q = useCatalogue()
  const enhanced = useFlag('capacity_screens')
  return (
    <Page title={t('nav.reports')} subtitle={<>{q.data && plural(q.data.length, 'reports.countOne', 'reports.count')}<PageHelp>{t('reports.help')}</PageHelp></>}>
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      {q.isPending ? <div className={card}><Loading rows={6} /></div> : q.data && (enhanced ? <ReportCatalogue reports={q.data} standalone /> :
        <TableRegion>
          <table className="w-full text-sm">
            <caption className="sr-only">{t('nav.reports')}</caption>
            <thead className="bg-muted"><tr><th scope="col" className={thCls}>{t('reports.report')}</th><th scope="col" className={thCls}>{t('common.description')}</th></tr></thead>
            <tbody>
              {q.data.map((r) => (
                <tr key={r.code} className="border-t hover:bg-muted">
                  <td className={cn(tdCls, 'whitespace-nowrap')}><Link to={r.code} className="font-medium hover:underline">{r.title}</Link></td>
                  <td className={cn(tdCls, 'text-muted-foreground')}>{r.description}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </TableRegion>
      )}
    </Page>
  )
}

/** One report: parameters in the URL, results as a table, export and "Open as filtered list" (§19). */
export function ReportPage() {
  const { code } = useParams()
  const cat = useCatalogue()
  const enhanced = useFlag('capacity_screens')
  const visualSystem = useFlag('visual_system')
  const compact = enhanced && visualSystem
  const peopleOn = useFlag('people_screens')
  const def = cat.data?.find((r) => r.code === code)
  const [sp, setSp] = useSearchParams()
  const chosen = useMemo(() => Object.fromEntries([...sp.entries()].filter(([k]) => !['taskInput', 'inputPage', 'inputPageSize', 'chartPeriod', 'chartPage', 'personStaffScope', 'personProjectsPage', 'personProjectsPageSize', 'recordTab', 'recordTabItem'].includes(k) && (enhanced ? def?.params.some(p => p.key === k) : k !== 'panel' && k !== 'history'))), [sp, enhanced, def])
  // An activity log without dates covers the last 30 days rather than the whole project history.
  const defaulted = code === 'project-activity-log' && sp.get('history') !== 'all' && !chosen.from && !chosen.to
  const params = useMemo(() => {
    if (defaulted) return { ...chosen, from: addDays(today(), -30), ...(enhanced ? { to: today() } : {}) }
    if (enhanced && code === 'project-activity-log' && sp.get('history') === 'all' && !chosen.from && !chosen.to) return { ...chosen, history: 'all' }
    if (code === 'tasks-due-this-week' && !chosen.week) return { ...chosen, week: addDays(today(), -((new Date(today() + 'T00:00:00Z').getUTCDay() + 6) % 7)) }
    return chosen
  }, [chosen, defaulted, code, enhanced, sp])
  const set = (k: string, v?: string | null) => { const n = clearReportInputState(new URLSearchParams(sp)); n.delete('chartPeriod'); n.delete('chartPage'); if (v) n.set(k, v); else n.delete(k); if (code === 'allocations-by-source' && k === 'show' && v === 'entries-without-project') { n.delete('unit'); n.delete('period') } if (code === 'project-activity-log' && (k === 'from' || k === 'to')) n.set('history', 'all'); n.delete('panel'); setSp(n, { replace: true }) }
  const optionalProject = enhanced && (code === 'workload-employee' || code === 'task-hours')
  const needsProject = !optionalProject && def?.params.some((p) => p.type === 'project') && !params.projectId
  // Which parameters a report takes is the catalogue's to say (each report names its own), so the contract declares no query for the run.
  const runKey = enhanced ? ['report', code, params, 'capacity_screens'] : ['report', code, params]
  const run = useQuery({ queryKey: peopleOn ? [...runKey, 'people_screens'] : runKey, queryFn: () => typedRequest('get', '/api/v1/reports/{code}', { params: { code: code! }, query: params as never }) as Promise<ReportRun>, enabled: !!def && !needsProject })
  const captured = enhanced && run.isSuccess && !run.isFetching && !needsProject ? run.data.savedParameters : undefined
  const viewProject = sp.get('viewProject') ?? undefined
  const fixed = Object.fromEntries(['catalogue', 'viewProject'].flatMap(key => sp.has(key) ? [[key, sp.get(key)!]] : []))
  if (cat.isPending) return <Page title={t('nav.reports')}><div className={card}><Loading rows={6} /></div></Page>
  if (!def) return <Page title={t('nav.reports')}>{cat.error ? <ErrorBanner error={cat.error} retry={() => cat.refetch()} /> : <div className={card}><Empty>{t('reports.unknown')}</Empty></div>}</Page>
  const controls = <>
      <form className="flex flex-wrap items-end gap-3 rounded-lg border bg-card p-4 md:p-5" onSubmit={(e) => e.preventDefault()} aria-label={t('reports.parameters')}>
        {def.params.filter(p => !(enhanced && code === 'allocations-by-source' && params.show === 'entries-without-project' && ['unit', 'period'].includes(p.key))).map((p) => <ParamInput key={p.key} p={p} value={params[p.key]} optionalProject={optionalProject} onChange={(v) => set(p.key, v)} />)}
        {code === 'project-activity-log' && <label className="flex min-h-(--control-h) items-center gap-2 text-sm"><Checkbox checked={!defaulted && !chosen.from && !chosen.to} onCheckedChange={on => {
          const n = clearReportInputState(new URLSearchParams(sp)); n.delete('from'); n.delete('to'); n.delete('panel'); if (on) n.set('history', 'all'); else n.delete('history'); setSp(n, { replace: true })
        }} />{t('reports.allHistory')}</label>}
        {Object.keys(chosen).length > 0 && <Button type="button" variant="ghost" onClick={() => setSp(enhanced ? new URLSearchParams(fixed) : new URLSearchParams(), { replace: true })}>{t('common.clear')}</Button>}
        {defaulted && <p className="w-full text-xs/[18px] text-muted-foreground">{t('reports.defaultRange')}</p>}
      </form>
      {enhanced && <section className="space-y-3 rounded-lg border bg-card p-4" aria-label={t('views.label')}>
        <div className="flex flex-wrap items-end gap-3">
          <Field label={t('reportScreens.viewProject')} htmlFor="report-view-project" className="w-full sm:max-w-72">
            <ProjectsPicker id="report-view-project" single value={viewProject} placeholder={t('reportScreens.personalOnly')} onChange={value => {
              const next = new URLSearchParams(sp); if (value) next.set('viewProject', value); else next.delete('viewProject'); next.delete('view'); setSp(next, { replace: true })
            }} />
          </Field>
          {viewProject && <Button type="button" variant="ghost" onClick={() => { const next = new URLSearchParams(sp); next.delete('viewProject'); next.delete('view'); setSp(next, { replace: true }) }}>{t('reportScreens.personalOnly')}</Button>}
          <ViewMenu key={`${code}:${viewProject ?? ''}`} listType={`report:${code}`} projectId={viewProject} fixed={fixed} captureReady={captured != null} captureParams={() => {
            if (captured == null) throw new Error(t('reportScreens.copyUnavailable'))
            return { ...captured }
          }} requireCompleteParams />
          {captured != null && <>
            <Button type="button" variant="outline" onClick={async () => {
              try {
                const target = new URL(`/reports/${encodeURIComponent(code!)}`, window.location.origin); target.search = new URLSearchParams({ ...captured, ...reportChartFocusParameters(code === 'team-capacity' ? run.data?.capacityComparison : undefined, sp) }).toString()
                await navigator.clipboard.writeText(target.toString()); toast.success(t('reportScreens.copied'))
              } catch (error) { toast.error((error as Error).message) }
            }}>{t('reportScreens.copy')}</Button>
          </>}
        </div>
        <p className="text-xs text-muted-foreground">{t('reportScreens.containerHint')}</p>
        <p className="text-xs text-muted-foreground">{t(captured == null ? 'reportScreens.copyUnavailable' : 'reportScreens.live')}</p>
      </section>}
  </>
  const resultNotes = <>{code === 'team-capacity' && <p className={compact ? undefined : 'mb-3 text-sm text-muted-foreground'}>{t('capacityReport.basis')}</p>}{run.data?.parameters.some(p => p.label === t('planner.partialTitle')) && <p className={compact ? undefined : 'mb-3 text-sm'}>{t('planner.partialHint')}</p>}</>
  const results = needsProject ? <div className={card}><Empty>{t('reports.chooseProject')}</Empty></div> : run.error ? <ErrorBanner error={run.error} retry={() => run.refetch()} /> : run.isPending || (enhanced && run.isFetching) ? <div className={card}><Loading rows={8} /></div> : (
    <>{!compact && resultNotes}<ReportTable r={run.data} itemType={def.itemType} asOf={run.dataUpdatedAt} enhanced={enhanced} compact={compact} compactNotes={compact ? resultNotes : undefined} /></>
  )
  return (
    <Page eyebrow={t('nav.reports')} title={def.title} subtitle={def.description} titleBlock={run.data && <TitleBlock asOf={(run.data as ReportRun & { asOf?: string }).asOf} />}
      actions={<>
        <Button variant="ghost" asChild><Link to={enhanced && sp.get('catalogue') ? `/reports?${new URLSearchParams({ catalogue: sp.get('catalogue')! })}` : '/reports'}><ArrowLeft className="size-4" />{t('nav.reports')}</Link></Button>
        {run.data?.listLink && <Button variant="outline" asChild><Link to={run.data.listLink}><ListFilter className="size-4" />{t('reports.openList')}</Link></Button>}
        {run.data && <ExportMenu path={`reports/${code}/export`} params={params} name={code!} />}
      </>}>
      {enhanced ? <div className="report-workspace"><ReportCatalogue reports={cat.data!} active={code} /><div className={compact ? 'report-results' : 'min-w-0 space-y-4'}>
        {/* A closed native disclosure keeps ViewMenu mounted so a saved default can supply required parameters. */}
        {compact ? <><details className="report-controls"><summary title={t('reportInputs.controlsHint')}>{t('reportInputs.controls')}</summary><div className="report-controls__body">{controls}</div></details><div className="report-results__body">{results}</div></> : <>{controls}{results}</>}
      </div></div> : <>{controls}{results}</>}
    </Page>
  )
}

function ReportCatalogue({ reports, active, standalone = false }: { reports: Schemas['ReportListItem'][]; active?: string; standalone?: boolean }) {
  const [sp, setSp] = useSearchParams(), location = useLocation(), phone = useIsPhone()
  const [expanded, setExpanded] = useState(false)
  const nav = useRef<HTMLElement>(null), summary = useRef<HTMLElement>(null)
  const search = sp.get('catalogue') ?? '', term = search.trim().toLocaleLowerCase()
  const groups = new Map<string, Schemas['ReportListItem'][]>()
  for (const report of reports) {
    if (term && ![report.title, report.question, report.description].some(value => value?.toLocaleLowerCase().includes(term))) continue
    const question = report.question ?? report.title
    const group = groups.get(question) ?? []; group.push(report); groups.set(question, group)
  }
  useEffect(() => {
    if ((location.state as { reportCatalogueFocus?: string } | null)?.reportCatalogueFocus !== active || !active) return
    if (phone && !expanded) summary.current?.focus({ preventScroll: true })
    else nav.current?.querySelector<HTMLElement>(`[data-report-code="${active}"]`)?.focus({ preventScroll: true })
  }, [active, location.key, location.state, phone, expanded])
  return <details className={cn('report-catalogue', standalone && 'report-catalogue--standalone')} open={!phone || standalone || expanded}
    onToggle={event => { if (phone && !standalone) setExpanded(event.currentTarget.open) }}>
    <summary ref={summary} className="report-catalogue__summary">{t('reportScreens.catalogue')}</summary>
    <nav ref={nav} aria-label={t('reportScreens.catalogue')} className="report-catalogue__body">
      <Field label={t('reportScreens.search')} htmlFor="report-catalogue-search"><Input id="report-catalogue-search" type="search" value={search} placeholder={t('reportScreens.searchHint')} onChange={event => {
        const next = new URLSearchParams(sp); if (event.target.value) next.set('catalogue', event.target.value); else next.delete('catalogue'); setSp(next, { replace: true })
      }} /></Field>
      {!groups.size ? <p role="status" className="text-sm text-muted-foreground">{t('reportScreens.noMatches')}</p> : <div className="report-catalogue__groups">{[...groups].map(([question, items]) => <section key={question}>
        <h2 className="mb-2 text-sm font-semibold">{question}</h2><ul className="space-y-1">{items.map(report => <li key={report.code}>
          <Link data-report-code={report.code} aria-current={active === report.code ? 'page' : undefined} state={{ reportCatalogueFocus: report.code }}
            className="report-catalogue__link" to={`/reports/${report.code}${search ? `?${new URLSearchParams({ catalogue: search })}` : ''}`}>
            <span className="font-medium">{report.title}</span><span className="text-xs text-muted-foreground">{report.description}</span>
          </Link>
        </li>)}</ul>
      </section>)}</div>}
    </nav>
  </details>
}

function ParamInput({ p, value, onChange, optionalProject }: { p: Param; value?: string; onChange: (v: string | null) => void; optionalProject?: boolean }) {
  const ref = useReference()
  const id = `rp-${p.key}`
  const box = (child: ReactNode, width = 'sm:w-44') => <Field label={p.label} htmlFor={id} className={cn('w-full', width)}>{child}</Field>
  switch (p.type) {
    case 'projects': return box(<ProjectsPicker id={id} value={value} onChange={onChange} />, 'sm:w-56')
    case 'project': return box(<ProjectsPicker id={id} value={value} onChange={onChange} single allowClear={optionalProject} placeholder={optionalProject ? t('reports.allProjects') : undefined} />, 'sm:w-56')
    case 'discipline': return box(<select id={id} className={selectCls} value={value ?? ''} onChange={(e) => onChange(e.target.value || null)}>
      <option value="">{t('projects.anyDiscipline')}</option>{ref.data?.disciplines.filter((d) => d.isActive).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}</select>)
    case 'office': return box(<select id={id} className={selectCls} value={value ?? ''} onChange={(e) => onChange(e.target.value || null)}>
      <option value="">{t('reports.anyOffice')}</option>{ref.data?.offices.filter((d) => d.isActive).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}</select>)
    case 'person': return box(<PeoplePicker id={id} value={value} onChange={(v) => onChange(v)} placeholder={t('reports.anyone')} label={p.label} />, 'sm:w-52')
    case 'date': return box(<Input id={id} type="date" value={value ?? ''} onChange={(e) => onChange(e.target.value || null)} />)
    case 'number': return box(<Input id={id} type="number" min={0} className="tabular-nums" placeholder={p.default ?? ''} value={value ?? ''} onChange={(e) => onChange(e.target.value || null)} />, 'sm:w-28')
    case 'bool': return <label className="flex min-h-(--control-h) items-center gap-2 text-sm"><Checkbox checked={(value ?? p.default) === 'true'} onCheckedChange={(c) => onChange(c ? 'true' : p.default === 'true' ? 'false' : null)} />{p.label}</label>
    default: return box(<select id={id} className={selectCls} value={value ?? p.default ?? ''} onChange={(e) => onChange(e.target.value || null)}>
      {!p.default && <option value="">{t('reports.any')}</option>}{p.options?.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</select>)
  }
}

/** Visible projects to scope a report; none chosen means every project the viewer can see. */
export function ProjectsPicker({ id, value, onChange, single, placeholder, allowClear }: { id: string; value?: string; onChange: (v: string | null) => void; single?: boolean; placeholder?: string; allowClear?: boolean }) {
  const [term, setTerm] = useState('')
  const [open, setOpen] = useState(false)
  const q = useQuery({ queryKey: ['projects-pick', term], queryFn: () => typedRequest('get', '/api/v1/projects', { query: { Q: term, PageSize: 50 } }) })
  const chosen = (value ?? '').split(',').filter(Boolean)
  const names = useQuery({ queryKey: ['projects-pick-ids', value], enabled: chosen.length > 0, queryFn: () => typedRequest('get', '/api/v1/projects', { query: { Ids: value, IncludeArchived: true, PageSize: 50 } }) })
  const label = chosen.length === 0 ? placeholder ?? (single ? t('reports.chooseProject') : t('reports.allProjects'))
    : (names.data?.items ?? []).filter((p) => chosen.includes(p.id)).map((p) => p.projectNumber).join(', ') || t('reports.nProjects', { n: chosen.length })
  const toggle = (pid: string) => {
    if (single) { onChange(pid); setOpen(false); return }
    const next = chosen.includes(pid) ? chosen.filter((x) => x !== pid) : [...chosen, pid]
    onChange(next.length ? next.join(',') : null)
  }
  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button id={id} variant="outline" className="w-full min-w-0 justify-between px-3 font-normal"><span className={cn('truncate', chosen.length === 0 && 'text-muted-foreground')}>{label}</span><ChevronsUpDown className="size-3.5 opacity-50" /></Button>
      </PopoverTrigger>
      <PopoverContent className="w-72 p-2" align="start">
        <Input type="search" className="mb-2" placeholder={t('common.search')} value={term} onChange={(e) => setTerm(e.target.value)} aria-label={t('common.search')} />
        <ul className="max-h-60 overflow-y-auto text-sm">
          {(q.data?.items ?? []).map((p) => (
            <li key={p.id}><label className="flex min-h-(--control-row-h) cursor-pointer items-center gap-2 rounded-md px-2 hover:bg-muted">
              {single ? <input type="radio" className="size-4 accent-(--primary)" name={id} checked={chosen.includes(p.id)} onChange={() => toggle(p.id)} /> : <Checkbox checked={chosen.includes(p.id)} onCheckedChange={() => toggle(p.id)} />}
              <Key>{p.projectNumber}</Key><span className="truncate">{p.name}</span>
            </label></li>
          ))}
        </ul>
        {(!single || allowClear) && chosen.length > 0 && <Button variant="ghost" size="sm" className="mt-1 w-full" onClick={() => { onChange(null); if (single) setOpen(false) }}>{allowClear ? t('reports.allProjects') : placeholder ? t('scope.clearChoice') : t('reports.allProjects')}</Button>}
      </PopoverContent>
    </Popover>
  )
}

const at = (row: Record<string, any>, path: string) => path.split('.').reduce<any>((v, k) => (v == null ? v : v[k]), row)
const NUMERIC = ['number', 'percent']

/** The result with what it covers: row count, the parameters as the server applied them, and when it was produced. */
function ReportTable({ r, itemType, asOf, enhanced, compact, compactNotes }: { r: ReportRun; itemType?: string | null; asOf: number; enhanced: boolean; compact: boolean; compactNotes?: ReactNode }) {
  const visualSystem = useFlag('visual_system')
  const people = useFlag('people_screens')
  const location = useLocation()
  const openPanel = useItemPanel()
  const units = enhanced ? r.unitView : undefined
  const comparison = enhanced && r.code === 'team-capacity' ? r.capacityComparison : undefined
  const withComparison = (matrix: ReactNode) => comparison ? <div className="report-capacity-layout"><div className="report-capacity-matrix">{matrix}</div><ReportCapacityComparison data={comparison} /></div> : matrix
  const columns = units?.columns ?? r.columns
  const unitCells = useMemo(() => new Map(units?.rows.flatMap(row => row.cells.map(cell => [`${row.rowIndex}:${cell.path}`, cell] as const)) ?? []), [units])
  const cell = (row: Record<string, any>, c: Column, rowIndex: number) => {
    const quantity = unitCells.get(`${rowIndex}:${c.path}`)
    if (quantity && units) return <ReportUnitValue value={quantity} unit={units.unit} />
    const v = at(row, c.path)
    // Activity rows carry their structured changes: show them as the Activity tab does, not as raw field names.
    if (c.path === 'action' && Array.isArray(row.categories)) return runtimeText(`action.${v}`)
    if (c.path === 'summary' && Array.isArray(row.changes)) return <ChangeList row={row as ActivityRow} />
    if (c.path === 'item' && row.itemType) return <><Key>{row.itemKey}</Key> {row.itemName || runtimeText(`itemType.${row.itemType}`)}</>
    if (v == null || v === '') return <Missing />
    const person = people ? reportUserReference(row, c.path) : undefined
    if (person) return <PersonPanelTrigger userId={person.id} name={person.name} active={person.active}
      focusKey={`report:${person.sourceKey}:${c.path}:cell`}>{typeof v === 'string' ? tv(v) : String(v)}</PersonPanelTrigger>
    switch (c.type) {
      case 'key': {
        const type = row.itemType ?? itemType
        return type && row.id && row.projectNumber
          ? <Link className="hover:underline" to={itemHref(type, row.projectNumber, row.id)} onClick={(e) => {
            if (e.button !== 0 || e.metaKey || e.ctrlKey || e.altKey || e.shiftKey) return
            if (enhanced && type === 'Task') { e.preventDefault(); openPanel(type, row.id, captureRecordContextOrigin(location, e.currentTarget)) }
            else if (location.pathname.startsWith(`/projects/${row.projectNumber}`)) { e.preventDefault(); openPanel(type, row.id) }
          }}><Key>{v}</Key></Link>
          : <Key>{v}</Key>
      }
      case 'date': return fmtDate(v)
      case 'datetime': return <time dateTime={v} title={v}>{fmtTime(v)}</time>
      case 'percent': return `${v}%`
      default: return c.path === 'status' || c.path === 'projectStatus' ? <StatusPill status={v} /> : typeof v === 'string' ? tv(v) : String(v)
    }
  }
  const rowCount = <span className="font-semibold text-foreground tabular-nums">{r.truncated ? t(r.totalIsLowerBound ? 'reports.truncatedLowerBound' : 'reports.truncated', { n: r.rows.length, total: r.total }) : plural(r.total, 'reports.row1', 'reports.rows')}</span>
  const appliedParameters = r.parameters.map((p, index) => <span key={`${p.label}:${index}`} className={compact ? 'report-results__parameter' : undefined}>{p.label}: <span className="font-medium text-foreground">{p.value}</span>{compact && ' '}</span>)
  return (
    <div className={compact ? 'report-results__sections' : 'space-y-3'}>
      {compact ? <div data-report-counts={r.code} className="report-results__header text-xs text-muted-foreground">
        <span className="report-results__count text-sm">{rowCount}</span>{' '}{appliedParameters}
        <div className="report-results__notes">{compactNotes}</div>
      </div> : <div data-report-counts={enhanced ? r.code : undefined} className="flex flex-wrap items-baseline gap-x-5 gap-y-1 text-sm text-muted-foreground">
        {rowCount}
        {appliedParameters}
        {!visualSystem && <span className="tabular-nums">{t('reports.asOf', { time: fmtTime(new Date(asOf).toISOString()) })}</span>}
      </div>}
      {enhanced && r.taskInputs && <ReportTaskInputs code={r.code} summary={r.taskInputs} applied={r.parameters} />}
      {withComparison(!r.rows.length ? <div className={card}><Empty>{t('reports.empty')}</Empty></div> : (
        // eslint-disable-next-line jsx-a11y/no-noninteractive-tabindex -- text-only rows: the region takes focus so the keyboard can scroll it
        <div className="scroll-region overflow-x-auto rounded-lg border bg-card" tabIndex={0} role="region" aria-label={r.title}>
          <table data-report-table={enhanced ? r.code : undefined} className="w-full text-sm">
            <thead className="bg-muted"><tr>{columns.map((c) => <th key={c.path} scope="col" className={cn(thCls, NUMERIC.includes(c.type) && 'text-right')}>{c.header}</th>)}</tr></thead>
            <tbody>
              {r.rows.map((row, i) => (
                <tr key={`${row.id ?? 'row'}:${i}`} className="border-t hover:bg-muted">
                  {columns.map((c) => <td key={c.path} className={cn(tdCls, NUMERIC.includes(c.type) ? 'text-right tabular-nums' : c.path === 'summary' ? 'min-w-64' : c.type === 'text' ? 'min-w-[8rem]' : 'whitespace-nowrap', ['date', 'datetime'].includes(c.type) && 'tabular-nums')}>{cell(row, c, i)}</td>)}
                </tr>
              ))}
            </tbody>
            {units?.rows.some(row => row.rowIndex === -1) && <tfoot><tr className="border-t bg-muted font-medium" data-report-unit-total>
              {columns.map((column, index) => <td key={column.path} className={cn(tdCls, NUMERIC.includes(column.type) && 'text-right tabular-nums')}>{index === 0 ? t('reportUnits.total') : cell({}, column, -1)}</td>)}
            </tr></tfoot>}
          </table>
        </div>
      ))}
      {units && <div role="note" className="report-units-basis text-xs text-muted-foreground" data-report-unit-basis>
        <p>{t('reportUnits.selection', { unit: t(units.unit === 'fte' ? 'reportUnits.fteLabel' : 'reportUnits.hoursLabel'), period: t(units.period === 'months' ? 'reportUnits.monthsLabel' : 'reportUnits.weeksLabel') })} {t('reportUnits.basis')}</p>
        <p>{units.standardWeeklyHours == null || units.standardWeeklyHours <= 0 ? t('reportUnits.unavailable') : t('reportUnits.standardLine', { n: units.standardWeeklyHours })} · {t('reportUnits.displayedWeeksLine', { n: units.displayedWeekCount })}</p>
        {units.period === 'months' && <><p>{t('reportUnits.thursday')}</p><ul className="flex flex-wrap gap-x-5 gap-y-1">{units.periods.map(period => <li key={period.key}>{t('reportUnits.periodBasisLine', { period: period.label, weeks: period.weekCount, hours: period.denominatorHours ?? t('reportUnits.unavailable') })}</li>)}</ul></>}
        {units.scopeCoverage === 'VisibleOnly' && <p>{t('reportUnits.partial')}</p>}
        <p>{t('reportUnits.missingLine', { n: units.unestimatedTasks })} · {t('reportUnits.counts')}</p><p>{t('reportUnits.rounding')}</p>
      </div>}
    </div>
  )
}

function ReportUnitValue({ value, unit }: { value: Schemas['ReportUnitCell']; unit: string }) {
  if (value.kind === 'NotApplicable') return <span className="text-muted-foreground" data-report-unit-not-applicable={value.path}><span aria-hidden>{t('common.dash')}</span><span className="sr-only">{t('value.Not Applicable')}</span></span>
  const hours = (n: number) => new Intl.NumberFormat(undefined, { maximumFractionDigits: 3 }).format(n)
  const numeric = value.kind === 'Hours' && unit === 'fte'
    ? new Intl.NumberFormat(undefined, { minimumFractionDigits: 1, maximumFractionDigits: 4 }).format(value.value ?? 0)
    : new Intl.NumberFormat(undefined, { maximumFractionDigits: value.kind === 'Count' ? 0 : 1 }).format(value.value ?? 0)
  const provisional = value.coverage === 'ProvisionalAvailability'
  return <span className="inline-grid gap-0.5" data-report-unit-cell={value.path} data-unit-kind={value.kind} data-unit-coverage={value.coverage} data-unit-value={value.value ?? undefined}>
    {value.value == null ? <><span>{provisional ? t('reportUnits.provisional') : value.coverage === 'StandardUnavailable' ? t('reportUnits.unavailable') : t('common.unavailable')}</span>{value.knownHours != null && <span className="text-xs text-muted-foreground">{t(provisional ? 'reportUnits.recorded' : 'reportUnits.known', { n: hours(value.knownHours) })}</span>}</> : <span>{numeric}</span>}
    {value.coverage === 'AvailabilityDependentCount' && <span className="text-xs text-muted-foreground">{t('reportUnits.availabilityCount')}</span>}
  </span>
}
