import { PersonPanelTrigger } from '@/components/hub/person-panel-trigger'
import { pageMode } from '@/lib/page-mode'
import { ProjectImportDialog } from '@/components/hub/project-import'
import { useQuery } from '@tanstack/react-query'
import { Columns3, Plus, Rows3, Star } from 'lucide-react'
import { useMemo, useRef, useState, type ReactNode } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { AccentDot, ActiveFilters, ChoiceMenu, Empty, ErrorBanner, Field, FilterBar, Loading, Missing, Page, Segmented, selectCls, tdCls, thCls } from '@/components/hub/common'
import { DataTable } from '@/components/hub/registers'
import type { Column } from '@/components/hub/table'
import { Chip, HealthPill, Key, PriorityBadge, ProgressBar, StatusPill } from '@/components/hub/pills'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { DropdownMenu, DropdownMenuCheckboxItem, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { useReference } from '@/hooks/data'
import { get, typedRequest } from '@/lib/api'
import { useMe, useFlag } from '@/lib/auth'
import { ago, fmtDate, relative } from '@/lib/format'
import { t, runtimeText, textFor } from '@/lib/i18n'
import type { components } from '@/gen/api'
import { accentOf, cn } from '@/lib/utils'
import { CreateProjectDialog } from './CreateProject'
import { ExportMenu } from '@/components/hub/export'
import { ViewMenu } from '@/components/hub/views'
import { HOME_PRESETS, ProjectAreaTabs } from '@/components/hub/workspace'
import { PeoplePicker } from '@/components/hub/people'
import { ProjectPeek, ProjectPlainProgress, ProjectSpan, ProjectStaffing } from './project-list-details'
import { ProjectBulkEdit, type SelectedProject } from './project-bulk-edit'
import type { SavedViewKeys } from '@/gen/registry.gen'

type ProjectRow = components['schemas']['ProjectRow']

const COLUMNS = ['client', 'pm', 'office', 'phase', 'status', 'health', 'priority', 'due', 'progress', 'nextMilestone', 'overdue', 'blocked', 'myRole', 'activity'] as const
type Col = (typeof COLUMNS)[number]
const DEFAULT_COLS: Col[] = ['client', 'pm', 'status', 'health', 'priority', 'due', 'progress', 'nextMilestone', 'overdue', 'blocked', 'myRole']
/** URL filters shown as removable tokens; scope (mine), grouping and sort are view choices, not filters. */
const FILTERS = ['q', 'status', 'health', 'officeId', 'disciplineId', 'leadUnitId', 'includeArchived', 'ids', 'computedHealth'] as const satisfies readonly (SavedViewKeys['projects'] | 'page' | 'panel' | 'draftAccessReason')[]
const VISUAL_FILTERS = ['clientId', 'pmId', 'phaseId', 'projectTypeId', 'submissionWithinDays', 'starred', 'priority'] as const
type ProjectCol = Column<ProjectRow> & { sortKey?: string; clearsSort?: boolean }
/** The page's URL filters (and the scope and page size) as the list's query; the server reads names without regard to case and ignores the rest. */
const projectQuery = (p: Record<string, string | boolean | number>) => ({
  Q: str(p.q), Status: str(p.status), PmId: str(p.pmId), ClientId: str(p.clientId), OfficeId: str(p.officeId), PhaseId: str(p.phaseId), DisciplineId: str(p.disciplineId), Health: str(p.health),
  ProjectTypeId: str(p.projectTypeId), IncludeArchived: flag(p.includeArchived), Mine: flag(p.mine), Starred: flag(p.starred), Priority: str(p.priority), Ids: str(p.ids), Sort: str(p.sort),
  Page: num(p.page), PageSize: num(p.pageSize), SubmissionWithinDays: num(p.submissionWithinDays), ComputedHealth: str(p.computedHealth), LeadUnitId: str(p.leadUnitId), HomePreset: str(p.homePreset), HomeProjects: str(p.homeProjects),
})
const str = (v: unknown) => (typeof v === 'string' && v !== '' ? v : undefined)
const flag = (v: unknown) => (v === true || v === 'true' ? true : v === 'false' ? false : undefined)
const num = (v: unknown) => (v === '' || v == null || Number.isNaN(Number(v)) ? undefined : Number(v))
const href = (r: ProjectRow) => `/projects/${encodeURIComponent(r.projectNumber)}`

/** Project list and grouped board presentation (§13.2, FR-021, §36.2 FR-VIS-03). Filters live in the URL. */
export function ProjectListPage() {
  const me = useMe()
  const [sp, setSp] = useSearchParams()
  const ref = useReference()
  const org = useQuery({ enabled: me.flags?.org_structure === true, queryKey: ['organisation'], queryFn: () => get<{ units: { id: string; name: string; parentId?: string; closedOn?: string }[] }>('organisation') })
  const navigate = useNavigate()
  const importEnabled = useFlag('project_import')
  const visualSystem = useFlag('visual_system')
  const projectScreens = useFlag('project_screens')
  const overview = useFlag('home_overview')
  const boardView = visualSystem && pageMode(sp, 'layout', ['board'], 'list') === 'board'
  const [importing, setImporting] = useState(false)
  const [creating, setCreating] = useState(false)
  const [selected, setSelected] = useState<Map<string, SelectedProject>>(() => new Map())
  const search = useRef<HTMLInputElement>(null)
  const [cols, setCols] = useState<Col[]>(() => JSON.parse(localStorage.getItem('hub.projectCols') ?? 'null') ?? DEFAULT_COLS)
  const filters = Object.fromEntries(sp.entries())
  const mine = sp.get('mine') !== 'false'
  const params = { ...filters, mine, pageSize: 200 }
  const list = useQuery({ queryKey: ['projects', params], queryFn: () => typedRequest('get', '/api/v1/projects', { query: projectQuery(params) }) })
  const set = (k: string, v?: string | null) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); setSp(n, { replace: true }) }
  const toggleCol = (c: Col) => { const n = cols.includes(c) ? cols.filter((x) => x !== c) : [...cols, c]; setCols(n); localStorage.setItem('hub.projectCols', JSON.stringify(n)) }
  const groupBy = sp.get('group') ?? 'status'
  const rows = list.data?.items ?? []
  const choose = (row: ProjectRow, checked: boolean) => {
    if (row.rowVersion == null) return
    const version = row.rowVersion
    setSelected(previous => {
      const next = new Map(previous)
      if (checked && !next.has(row.id)) next.set(row.id, { id: row.id, projectNumber: row.projectNumber, name: row.name, rowVersion: version })
      else if (!checked) next.delete(row.id)
      return next
    })
  }
  const selection = (row: ProjectRow) => <Checkbox checked={selected.has(row.id)} disabled={row.rowVersion == null} title={row.rowVersion == null ? t('projectBulk.versionUnavailable') : undefined}
    aria-label={t('projectBulk.select', { project: row.projectNumber })} onClick={event => event.stopPropagation()} onCheckedChange={checked => choose(row, checked === true)} />
  const selectableRows = rows.filter(row => row.rowVersion != null)
  const selectedShown = selectableRows.filter(row => selected.has(row.id)).length
  const selectShown = (checked: boolean) => setSelected(previous => {
    const next = new Map(previous)
    for (const row of selectableRows) {
      if (checked && !next.has(row.id)) next.set(row.id, { id: row.id, projectNumber: row.projectNumber, name: row.name, rowVersion: row.rowVersion! })
      else if (!checked) next.delete(row.id)
    }
    return next
  })
  const groups = useMemo(() => {
    if (projectScreens && ['pm', 'client', 'office'].includes(groupBy)) {
      const grouped = new Map<string, { key: string; label: string; rows: ProjectRow[] }>()
      for (const r of rows) {
        const [key, label] = groupBy === 'pm' ? [r.pm.id, r.pm.displayName] : groupBy === 'client' ? [r.client.id, r.client.name] : [r.officeId, r.office]
        const current = grouped.get(key)
        if (current) current.rows.push(r)
        else grouped.set(key, { key, label, rows: [r] })
      }
      return [...grouped.values()].sort((a, b) => a.label.localeCompare(b.label) || a.key.localeCompare(b.key))
    }
    if (groupBy !== 'status') return [{ key: 'all', label: t('projects.all'), rows }]
    const g = [
      { key: 'Active', label: t('projects.group.active'), rows: rows.filter((r) => r.status === 'Active') },
      { key: 'Setup', label: t('projects.group.upcoming'), rows: rows.filter((r) => r.status === 'Setup') },
      { key: 'On Hold', label: t('value.On Hold'), rows: rows.filter((r) => r.status === 'On Hold') },
      { key: 'Closed', label: t('projects.group.closed'), rows: rows.filter((r) => ['Complete', 'Archived', 'Cancelled'].includes(r.status)) },
    ]
    return g.filter((x) => x.rows.length > 0)
  }, [rows, groupBy, projectScreens])
  const sort = sp.get('sort')
  const sortBy = (k: string) => set('sort', sort === `${k}:asc` ? `${k}:desc` : `${k}:asc`)
  const dir = (k: string) => (sort === `${k}:asc` ? 'ascending' : sort === `${k}:desc` ? 'descending' : 'none')
  const ordered = groups.flatMap((g) => g.rows)
  const groupKeyOf = new Map(groups.flatMap((g) => g.rows.map((r) => [r.id, g.key] as const)))
  const groupLabel = new Map(groups.map(g => [g.key, g.label]))
  const groupTitle = (key: string, label: string, surface: 'table' | 'cards' | 'board') => {
    const group = groups.find(g => g.key === key), pm = group?.rows[0]?.pm
    if (!projectScreens || groupBy !== 'pm' || !group || !pm?.id || pm.id === '00000000-0000-0000-0000-000000000000'
      || pm.id !== key || !pm.displayName.trim() || pm.displayName !== label
      || !group.rows.every(row => row.pm.id === key && row.pm.displayName === label)) return label
    return <PersonPanelTrigger userId={pm.id} name={pm.displayName} focusKey={`project-list:group:pm:${key}:${surface}:heading`}>{label}</PersonPanelTrigger>
  }
  const columns: ProjectCol[] = [
    ...(projectScreens ? [{ id: 'select', label: t('projectBulk.selectShown'), fixed: true, className: 'w-10 px-2', cell: selection }] : []),
    { id: 'star', label: t('projects.star'), fixed: true, className: 'w-12 px-2', cell: (r) => <StarButton row={r} onDone={() => list.refetch()} /> },
    { id: 'number', label: t('projects.col.number'), fixed: true, sortKey: 'number', className: 'whitespace-nowrap', cell: (r) => <span className="inline-flex items-center gap-2"><AccentDot id={r.id} /><Key>{r.projectNumber}</Key></span> },
    { id: 'name', label: t('common.name'), fixed: true, sortKey: 'name', className: 'min-w-48',
      cell: (r) => <Link to={href(r)} onClick={(e) => e.stopPropagation()} className="break-words font-semibold hover:underline">{r.name}</Link> },
    { id: 'client', label: t('projects.col.client'), sortKey: 'client', cell: (r) => r.client.name },
    { id: 'pm', label: t('projects.col.pm'), sortKey: 'pm', className: 'whitespace-nowrap', cell: (r) => r.pm.id && r.pm.id !== '00000000-0000-0000-0000-000000000000' && r.pm.displayName.trim() ? <PersonPanelTrigger userId={r.pm.id} name={r.pm.displayName} focusKey={`project-list:${r.id}:pm:table`} /> : r.pm.displayName },
    { id: 'office', label: t('projects.col.office'), cell: (r) => r.office ?? <Missing /> },
    { id: 'phase', label: t('projects.col.phase'), cell: (r) => r.phase ?? <Missing /> },
    { id: 'status', label: t('projects.col.status'), sortKey: 'status', cell: (r) => <StatusPill status={r.status} /> },
    { id: 'health', label: t('projects.col.health'), clearsSort: true, cell: (r) => <>
      <Link to={`${href(r)}?why=health`} onClick={(e) => e.stopPropagation()} className="inline-flex rounded-md"><HealthPills r={r} /><span className="sr-only">{t('common.why')}</span></Link>
      <OverrideNote r={r} />
    </> },
    { id: 'priority', label: t('projects.col.priority'), sortKey: 'priority', cell: (r) => <PriorityBadge priority={r.priority} /> },
    { id: 'due', label: t('projects.col.due'), sortKey: 'due', className: 'whitespace-nowrap tabular-nums', cell: (r) => fmtDate(r.targetCompletionDate) },
    { id: 'progress', label: t('projects.col.progress'), fixed: projectScreens, sortKey: 'progress', cell: (r) => projectScreens ? <ProjectPlainProgress row={r} /> : <ProgressBar pct={r.progressPct} /> },
    { id: 'nextMilestone', label: t('projects.col.nextMilestone'), className: 'min-w-44', cell: (r) => <NextMilestone r={r} /> },
    { id: 'overdue', label: t('projects.col.overdue'), sortKey: 'overdue', className: 'text-right tabular-nums', cell: (r) => <span className={cn(r.overdueTasks > 0 && 'font-semibold text-bad')}>{r.overdueTasks}</span> },
    { id: 'blocked', label: t('projects.col.blocked'), className: 'text-right tabular-nums', cell: (r) => <span className={cn(r.blockedTasks > 0 && 'font-semibold text-bad')}>{r.blockedTasks}</span> },
    { id: 'myRole', label: t('projects.col.myRole'), cell: (r) => r.myRoles.map((x) => runtimeText(`role.${x}`)).join(', ') },
    { id: 'activity', label: t('projects.col.activity'), sortKey: 'activity', className: 'whitespace-nowrap text-xs/[18px] text-muted-foreground', cell: (r) => ago(r.lastActivityAt) },
    ...(projectScreens ? [
      { id: 'staffing', label: t('projectScreens.staffing'), fixed: true, cell: (r: ProjectRow) => <ProjectStaffing row={r} /> },
      { id: 'span', label: t('projectScreens.span'), fixed: true, cell: (r: ProjectRow) => <ProjectSpan row={r} /> },
      { id: 'peek', label: t('projectScreens.peek'), fixed: true, cell: (r: ProjectRow) => <Button size="sm" variant="ghost" aria-label={t('projectScreens.peekProject', { project: r.projectNumber })} onClick={e => { e.stopPropagation(); set('peek', r.id) }}>{t('projectScreens.peek')}</Button> },
    ] : []),
  ]
  const table = {
    visible: columns.filter((c) => c.fixed || cols.includes(c.id as Col)),
    header: (c: ProjectCol) => c.id === 'select'
      ? <th key={c.id} scope="col" className="w-10 px-2"><Checkbox aria-label={t('projectBulk.selectShown')} disabled={!selectableRows.length} checked={selectedShown === 0 ? false : selectedShown === selectableRows.length ? true : 'indeterminate'} onCheckedChange={checked => selectShown(checked === true)} /></th>
      : c.id === 'star'
      ? <th key={c.id} scope="col" className="w-12 px-2"><span className="sr-only">{c.label}</span></th>
      : <Th key={c.id} label={c.label} numeric={c.className?.includes('text-right')}
          {...(c.sortKey ? { sort: dir(c.sortKey), onSort: () => sortBy(c.sortKey!) } : c.clearsSort ? { onSort: () => set('sort', null) } : {})} />,
    cell: (c: ProjectCol, r: ProjectRow) => <td key={c.id} className={cn(tdCls, c.className)}>{c.cell(r)}</td>,
  }
  const ids = sp.get('ids')
  const filterKeys: readonly string[] = [...(visualSystem || projectScreens ? [...FILTERS, ...VISUAL_FILTERS] : FILTERS), ...(overview ? ['homePreset', 'homeProjects'] : [])]
  const tokens = ([
    ['q', t('common.search'), sp.get('q')],
    ['homePreset', t('homeScope.label'), textFor(HOME_PRESETS, sp.get('homePreset') ?? '')],
    ['homeProjects', t('homeScope.projects'), sp.get('homeProjects') === 'all' ? t('homeScope.allProjects') : sp.get('homeProjects') === 'mine' ? t('homeScope.myProjects') : sp.get('homeProjects') === 'none' ? t('homeScope.noProjects') : t('homeScope.chosenProjects', { n: sp.get('homeProjects')?.split(',').filter(Boolean).length ?? 0 })],
    ['status', t('common.status'), sp.get('status') && runtimeText(`value.${sp.get('status')}`)],
    ['health', t('projects.health'), sp.get('health') && runtimeText(`health.${sp.get('health')}`)],
    ['officeId', t('admin.office'), ref.data?.offices.find((o) => o.id === sp.get('officeId'))?.name],
    ['disciplineId', t('common.discipline'), ref.data?.disciplines.find((d) => d.id === sp.get('disciplineId'))?.name],
    ['leadUnitId', t('org.leadUnit'), org.data?.units.find((u) => u.id === sp.get('leadUnitId'))?.name ?? (sp.get('leadUnitId') ? t('admin.structure.noUnit') : undefined)],
    ['includeArchived', t('projects.includeArchived'), t('common.yes')],
    ['ids', t('projects.filter.chosen'), ids && ids.split(',').filter((x) => x && x !== 'none').length],
    ['computedHealth', t('projects.filter.computedHealth'), sp.get('computedHealth')?.split(',').map((h) => runtimeText(`health.${h}`)).join(', ')],
    ['clientId', t('portfolio.client'), ref.data?.clients.find(x => x.id === sp.get('clientId'))?.name ?? t('common.unavailable')],
    ['pmId', t('portfolio.pm'), rows.find(x => x.pm.id === sp.get('pmId'))?.pm.displayName ?? t('portfolio.selectedPm')],
    ['phaseId', t('field.PhaseId'), ref.data?.phases.find(x => x.id === sp.get('phaseId'))?.name ?? t('common.unavailable')],
    ['projectTypeId', t('projects.type'), ref.data?.projectTypes.find(x => x.id === sp.get('projectTypeId'))?.name ?? t('common.unavailable')],
    ['submissionWithinDays', t('portfolio.within'), sp.get('submissionWithinDays')],
    ['starred', t('projects.star'), sp.get('starred') === 'true' ? t('common.yes') : t('common.no')],
    ['priority', t('projects.col.priority'), sp.get('priority')],
  ] as const).filter(([k]) => filterKeys.includes(k) && sp.get(k))
  // The search box stays uncontrolled (URL updates arrive in a transition), so removing its filter empties it here.
  const remove = (k: string) => { if (k === 'q' && search.current) search.current.value = ''; set(k, null) }
  const clear = () => { if (search.current) search.current.value = ''; const n = new URLSearchParams(sp); for (const k of filterKeys) n.delete(k); setSp(n, { replace: true }) }
  const create = me.capabilities.createProject && <Button variant="outline" onClick={() => setCreating(true)}><Plus className="size-4" />{t('projects.create')}</Button>

  const emptyState = <Empty title={t('projects.emptyTitle')} action={(tokens.length > 0 || mine || create) && (
          <div className="flex flex-wrap justify-center gap-2">
            {tokens.length > 0 && <Button variant="outline" onClick={clear}>{t('filters.clear')}</Button>}
            {mine && <Button variant="outline" onClick={() => set('mine', 'false')}>{t('projects.all')}</Button>}
            {create}
          </div>
        )}>{mine ? t('projects.emptyMine') : t('projects.empty')}</Empty>

  return (
    <Page title={t('nav.projects')} subtitle={t('projects.subtitle')} tabs={<ProjectAreaTabs />} actions={<>
      {me.capabilities.admin && importEnabled && <DropdownMenu><DropdownMenuTrigger asChild><Button variant="outline">{t('common.more')}</Button></DropdownMenuTrigger><DropdownMenuContent><DropdownMenuItem onSelect={() => setImporting(true)}>{t('projectImport.title')}</DropdownMenuItem></DropdownMenuContent></DropdownMenu>}
      <ViewMenu listType="projects" />
      <ExportMenu path="projects/export" params={{ ...filters, mine }} name="projects" />
      {me.capabilities.createProject && <Button onClick={() => setCreating(true)}><Plus className="size-4" />{t('projects.create')}</Button>}
    </>}>
      {importing && <ProjectImportDialog onClose={() => setImporting(false)} onChanged={() => list.refetch()} />}
      {projectScreens && selected.size > 0 && <ProjectBulkEdit selected={[...selected.values()]} clear={() => setSelected(new Map())} changed={() => { void list.refetch() }} />}
      <FilterBar activeCount={tokens.length}
        search={<Field label={t('common.search')} htmlFor="pl-search">
          <Input ref={search} id="pl-search" type="search" placeholder={t('projects.search')} defaultValue={sp.get('q') ?? ''} onChange={(e) => set('q', e.target.value)} />
        </Field>}
        quick={<Segmented label={t('projects.scope')} value={mine ? 'mine' : 'all'} onChange={(v) => set('mine', v === 'mine' ? null : 'false')}
          options={[{ value: 'mine', label: t('projects.mine') }, { value: 'all', label: t('projects.all') }]} />}
        active={<ActiveFilters tokens={tokens.map(([key, label, value]) => ({ key, label, value: value ?? t('common.dash') }))} onRemove={remove} onClear={clear} />}
        actions={<>
          <ChoiceMenu label={t('common.groupBy')} icon={Rows3} value={projectScreens && ['pm', 'client', 'office'].includes(groupBy) ? groupBy : groupBy === 'status' ? 'status' : 'none'} onChange={(v) => set('group', v === 'status' ? null : v)}
            options={[{ value: 'status', label: t('common.status') }, { value: 'none', label: t('common.none') }, ...(projectScreens ? [
              { value: 'pm', label: t('portfolio.pm') }, { value: 'client', label: t('projects.col.client') }, { value: 'office', label: t('projects.col.office') },
            ] : [])]} />
          <DropdownMenu>
            <DropdownMenuTrigger asChild><Button variant="outline" className="hidden md:inline-flex"><Columns3 className="size-4" />{t('common.columns')}</Button></DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              {COLUMNS.filter(c => !projectScreens || c !== 'progress').map((c) => <DropdownMenuCheckboxItem key={c} checked={cols.includes(c)} onCheckedChange={() => toggleCol(c)} onSelect={(e) => e.preventDefault()}>{t(`projects.col.${c}`)}</DropdownMenuCheckboxItem>)}
            </DropdownMenuContent>
          </DropdownMenu>
        </>}>
        <div className="flex flex-wrap items-end gap-3">
          <Field label={t('common.status')} htmlFor="pl-status" className="w-full sm:w-48">
            <select id="pl-status" className={selectCls} value={sp.get('status') ?? ''} onChange={(e) => set('status', e.target.value)}>
              <option value="">{t('projects.liveStatuses')}</option>
              {['Setup', 'Active', 'On Hold', 'Complete', 'Archived', 'Cancelled'].map((s) => <option key={s} value={s}>{runtimeText(`value.${s}`)}</option>)}
            </select>
          </Field>
          <Field label={t('projects.health')} htmlFor="pl-health" className="w-full sm:w-40">
            <select id="pl-health" className={selectCls} value={sp.get('health') ?? ''} onChange={(e) => set('health', e.target.value)}>
              <option value="">{t('projects.anyHealth')}</option>
              {['Red', 'Yellow', 'Green', 'Grey'].map((h) => <option key={h} value={h}>{runtimeText(`health.${h}`)}</option>)}
            </select>
          </Field>
          <Field label={t('admin.office')} htmlFor="pl-office" className="w-full sm:w-44">
            <select id="pl-office" className={selectCls} value={sp.get('officeId') ?? ''} onChange={(e) => set('officeId', e.target.value)}>
              <option value="">{t('projects.anyOffice')}</option>
              {ref.data?.offices.map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
            </select>
          </Field>
          <Field label={t('common.discipline')} htmlFor="pl-discipline" className="w-full sm:w-48">
            <select id="pl-discipline" className={selectCls} value={sp.get('disciplineId') ?? ''} onChange={(e) => set('disciplineId', e.target.value)}>
              <option value="">{t('projects.anyDiscipline')}</option>
              {ref.data?.disciplines.filter((d) => d.isActive).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
          </Field>
          {projectScreens && <>
            <Field label={t('portfolio.pm')} htmlFor="pl-pm" className="w-full sm:w-48"><PeoplePicker id="pl-pm" value={sp.get('pmId')} valueName={rows.find(r => r.pm.id === sp.get('pmId'))?.pm.displayName} onChange={id => set('pmId', id)} placeholder={t('portfolio.anyPm')} label={t('portfolio.pm')} /></Field>
            <Field label={t('projects.col.client')} htmlFor="pl-client" className="w-full sm:w-44"><select id="pl-client" className={selectCls} value={sp.get('clientId') ?? ''} onChange={e => set('clientId', e.target.value)}><option value="">{t('projectScreens.anyClient')}</option>{ref.data?.clients.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></Field>
            <Field label={t('projects.col.phase')} htmlFor="pl-phase" className="w-full sm:w-44"><select id="pl-phase" className={selectCls} value={sp.get('phaseId') ?? ''} onChange={e => set('phaseId', e.target.value)}><option value="">{t('projectScreens.anyPhase')}</option>{ref.data?.phases.filter(x => x.isActive || x.id === sp.get('phaseId')).map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></Field>
            <Field label={t('projects.type')} htmlFor="pl-type" className="w-full sm:w-44"><select id="pl-type" className={selectCls} value={sp.get('projectTypeId') ?? ''} onChange={e => set('projectTypeId', e.target.value)}><option value="">{t('projectScreens.anyType')}</option>{ref.data?.projectTypes.filter(x => x.isActive || x.id === sp.get('projectTypeId')).map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></Field>
            <Field label={t('projects.col.priority')} htmlFor="pl-priority" className="w-full sm:w-40"><select id="pl-priority" className={selectCls} value={sp.get('priority') ?? ''} onChange={e => set('priority', e.target.value)}><option value="">{t('projectScreens.anyPriority')}</option>{(['Low', 'Medium', 'High', 'Critical'] as const).map(value => <option key={value} value={value}>{t(`value.${value}`)}</option>)}</select></Field>
          </>}
          {me.flags?.org_structure === true && <Field label={t('org.leadUnit')} htmlFor="pl-lead-unit" className="w-full sm:w-48">
            <select id="pl-lead-unit" className={selectCls} value={sp.get('leadUnitId') ?? ''} onChange={(e) => set('leadUnitId', e.target.value)}>
              <option value="">{t('org.anyUnit')}</option><option value="00000000-0000-0000-0000-000000000000">{t('admin.structure.noUnit')}</option>{org.data?.units.filter((u) => !u.closedOn).map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}
            </select>
          </Field>}
          <label className="flex min-h-(--control-h) items-center gap-2 text-sm">
            <Checkbox checked={sp.get('includeArchived') === 'true'} onCheckedChange={(c) => set('includeArchived', c ? 'true' : null)} />{t('projects.includeArchived')}
          </label>
        </div>
      </FilterBar>

      {list.error && <ErrorBanner error={list.error} retry={() => list.refetch()} />}
      {projectScreens && list.data && list.data.totalCount > rows.length && <p className="text-xs text-muted-foreground">{t('projectScreens.shownProjects', { n: rows.length, total: list.data.totalCount })}</p>}
      {boardView ? list.isPending ? <Loading rows={6} /> : !list.error && ordered.length === 0 ? emptyState : <div data-project-board className="flex min-w-0 gap-4 overflow-x-auto pb-2">
        {groups.map((g) => <section key={g.key} aria-labelledby={`pl-board-${g.key}`} className="flex min-w-72 max-w-80 flex-1 flex-col rounded-lg border bg-muted/40">
          <h2 id={`pl-board-${g.key}`} className="flex shrink-0 items-center justify-between border-b px-3 py-2 text-sm font-semibold">{groupTitle(g.key, g.label, 'board')}<span className="rounded-md bg-card px-2 py-0.5 text-xs tabular-nums">{g.rows.length}</span></h2>
          <ul className="min-h-24 space-y-2 overflow-y-auto p-2">{g.rows.map((r) => <ProjectCard key={r.id} r={r} onStar={() => list.refetch()} onPeek={projectScreens ? () => set('peek', r.id) : undefined} selection={projectScreens ? selection(r) : undefined} />)}</ul>
        </section>)}
      </div> : <DataTable table={table} rows={ordered} loading={list.isPending} caption={t('nav.projects')} current={() => false} onRowClick={(r) => projectScreens ? set('peek', r.id) : navigate(href(r))}
        groupBy={groupBy === 'status' || projectScreens && ['pm', 'client', 'office'].includes(groupBy) ? (r) => groupKeyOf.get(r.id) ?? '' : undefined}
        groupRow={(label, count, span) => (
          <tr className="border-t">
            <th scope="rowgroup" colSpan={span} className="bg-sidebar px-(--cell-px) py-2 text-left text-xs/[18px] font-semibold uppercase tracking-[0.5px] text-muted-foreground">
              {groupTitle(label, groupLabel.get(label) ?? label, 'table')} <span className="ml-1 rounded-md bg-card px-2 py-0.5 font-medium normal-case tracking-normal tabular-nums">{count}</span>
            </th>
          </tr>
        )}
        empty={emptyState}
        /* Phones (§13.0): the list becomes cards with the project's identity stripe. */
        cards={<div className="space-y-6 md:hidden">
          {groups.map((g) => (
            <section key={g.key} aria-labelledby={`pl-g-${g.key}`} className="space-y-2">
              <h2 id={`pl-g-${g.key}`} className="text-xs/[18px] font-semibold uppercase tracking-[0.5px] text-muted-foreground">
                {groupTitle(g.key, g.label, 'cards')} <span className="ml-1 rounded-md bg-secondary px-2 py-0.5 font-medium normal-case tracking-normal tabular-nums">{g.rows.length}</span>
              </h2>
              <ul className="space-y-2">{g.rows.map((r) => <ProjectCard key={r.id} r={r} onStar={() => list.refetch()} onPeek={projectScreens ? () => set('peek', r.id) : undefined} selection={projectScreens ? selection(r) : undefined} />)}</ul>
            </section>
          ))}
        </div>} />}
      {creating && <CreateProjectDialog onClose={() => setCreating(false)} />}
      {projectScreens && sp.get('peek') && <ProjectPeek key={sp.get('peek')} id={sp.get('peek')!} close={() => set('peek', null)} />}
    </Page>
  )
}

function Th({ label, sort, onSort, numeric }: { label: string; sort?: 'ascending' | 'descending' | 'none'; onSort?: () => void; numeric?: boolean }) {
  return (
    <th scope="col" className={cn(thCls, numeric && 'text-right')} aria-sort={onSort ? sort ?? 'none' : undefined}>
      {onSort ? (
        <button type="button" className="inline-flex items-center gap-1 hover:text-foreground" onClick={onSort}>
          {label}{sort && sort !== 'none' && <span aria-hidden>{sort === 'descending' ? '↓' : '↑'}</span>}
        </button>
      ) : label}
    </th>
  )
}

/** Health; when computed and reported differ both show, labelled and never merged: "Computed Yellow · Reported Green
 *  (PM note, 3 days ago)" (§16.4). */
function HealthPills({ r }: { r: ProjectRow }) {
  const differs = r.reportedHealth !== r.computedHealth
  return (
    <span className="flex flex-wrap gap-1">
      <HealthPill health={differs ? r.computedHealth : r.reportedHealth} label={differs ? t('health.computed') : undefined} />
      {differs && <HealthPill health={r.reportedHealth} label={t('health.reported')} />}
    </span>
  )
}

/** The PM's note behind a reported health that differs from the computed one, and how old it is (§16.4). */
function OverrideNote({ r }: { r: ProjectRow }) {
  if (r.reportedHealth === r.computedHealth || !r.healthOverrideNote) return null
  return <p className="mt-1 max-w-56 truncate text-xs/[18px] text-muted-foreground" title={r.healthOverrideNote}>{r.healthOverrideNote}{r.healthOverrideAt && ` · ${ago(r.healthOverrideAt)}`}</p>
}

function NextMilestone({ r }: { r: ProjectRow }) {
  const m = r.nextMilestone
  if (!m) return <Missing />
  return (
    <>
      <span className="break-words"><span aria-hidden className="mr-1 text-muted-foreground">◆</span>{m.name}</span>
      <span className="block whitespace-nowrap text-xs/[18px] text-muted-foreground tabular-nums">{fmtDate(m.date)} · {relative(m.date)}</span>
    </>
  )
}

function ProjectCard({ r, onStar, onPeek, selection }: { r: ProjectRow; onStar: () => void; onPeek?: () => void; selection?: ReactNode }) {
  return (
    <li data-accent={accentOf(r.id)} className="rounded-lg border border-t-4 border-t-(color:--acc-stripe) bg-card p-4 text-sm">
      <div className="flex items-start gap-2">
        <div className="min-w-0 flex-1">
          <Key>{r.projectNumber}</Key>
          <Link to={href(r)} className="block break-words text-base/6 font-semibold hover:underline">{r.name}</Link>
          <p className="text-muted-foreground">{r.client.name} · {r.pm.id && r.pm.id !== '00000000-0000-0000-0000-000000000000' && r.pm.displayName.trim() ? <PersonPanelTrigger userId={r.pm.id} name={r.pm.displayName} focusKey={`project-list:${r.id}:pm:card`} /> : r.pm.displayName}</p>
        </div>
        {selection}<StarButton row={r} onDone={onStar} />
      </div>
      <div className="mt-3 flex flex-wrap items-center gap-1.5">
        <StatusPill status={r.status} />
        <HealthPills r={r} />
        {r.overdueTasks > 0 && <Chip tone="bad">{t('ind.overdueN', { n: r.overdueTasks })}</Chip>}
        {r.blockedTasks > 0 && <Chip tone="bad">{t('ind.blockedN', { n: r.blockedTasks })}</Chip>}
      </div>
      <OverrideNote r={r} />
      <dl className="mt-3 grid grid-cols-[auto_1fr] gap-x-3 gap-y-1">
        <dt className="text-muted-foreground">{t('projects.col.phase')}</dt><dd>{r.phase ?? <Missing />}</dd>
        <dt className="text-muted-foreground">{t('projects.col.nextMilestone')}</dt><dd><NextMilestone r={r} /></dd>
      </dl>
      {onPeek && <div className="mt-3 space-y-3 border-t pt-3"><ProjectPlainProgress row={r} /><ProjectStaffing row={r} /><ProjectSpan row={r} /><Button variant="outline" size="sm" onClick={onPeek} aria-label={t('projectScreens.peekProject', { project: r.projectNumber })}>{t('projectScreens.peek')}</Button></div>}
    </li>
  )
}

function StarButton({ row, onDone }: { row: ProjectRow; onDone: () => void }) {
  return (
    <button type="button" aria-label={row.starred ? t('projects.unstar') : t('projects.star')} aria-pressed={row.starred}
      onClick={async (e) => { e.stopPropagation(); await typedRequest(row.starred ? 'delete' : 'post', '/api/v1/projects/{id}/star', { params: { id: row.id } }); onDone() }}
      className={cn('grid size-(--control-row-h) shrink-0 place-items-center rounded-md hover:bg-secondary', row.starred ? 'text-warn' : 'text-muted-foreground')}>
      <Star className={cn('size-4', row.starred && 'fill-current')} />
    </button>
  )
}
