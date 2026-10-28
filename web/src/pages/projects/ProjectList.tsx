import { ProjectImportDialog } from '@/components/hub/project-import'
import { useQuery } from '@tanstack/react-query'
import { Columns3, Plus, Rows3, Star } from 'lucide-react'
import { useMemo, useRef, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { AccentDot, ActiveFilters, ChoiceMenu, Empty, ErrorBanner, Field, FilterBar, Missing, Page, Segmented, selectCls, tdCls, thCls } from '@/components/hub/common'
import { DataTable } from '@/components/hub/registers'
import type { Column } from '@/components/hub/table'
import { Chip, HealthPill, Key, PriorityBadge, ProgressBar, StatusPill } from '@/components/hub/pills'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { DropdownMenu, DropdownMenuCheckboxItem, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { useReference } from '@/hooks/data'
import { del, get, post, qs } from '@/lib/api'
import { useMe, useFlag } from '@/lib/auth'
import { ago, fmtDate, relative } from '@/lib/format'
import { t , runtimeText } from '@/lib/i18n'
import type { Page as P, ProjectRow } from '@/lib/types'
import { accentOf, cn } from '@/lib/utils'
import { CreateProjectDialog } from './CreateProject'
import { ExportMenu } from '@/components/hub/export'
import { ViewMenu } from '@/components/hub/views'
import type { SavedViewKeys } from '@/gen/registry.gen'

const COLUMNS = ['client', 'pm', 'office', 'phase', 'status', 'health', 'priority', 'due', 'progress', 'nextMilestone', 'overdue', 'blocked', 'myRole', 'activity'] as const
type Col = (typeof COLUMNS)[number]
const DEFAULT_COLS: Col[] = ['client', 'pm', 'status', 'health', 'priority', 'due', 'progress', 'nextMilestone', 'overdue', 'blocked', 'myRole']
/** URL filters shown as removable tokens; scope (mine), grouping and sort are view choices, not filters. */
const FILTERS = ['q', 'status', 'health', 'officeId', 'disciplineId', 'leadUnitId', 'includeArchived', 'ids', 'computedHealth'] as const satisfies readonly (SavedViewKeys['projects'] | 'page' | 'panel' | 'draftAccessReason')[]
type ProjectCol = Column<ProjectRow> & { sortKey?: string; clearsSort?: boolean }
const href = (r: ProjectRow) => `/projects/${encodeURIComponent(r.projectNumber)}`

/** Project list and grouped board presentation (§13.2, FR-021, §36.2 FR-VIS-03). Filters live in the URL. */
export function ProjectListPage() {
  const me = useMe()
  const [sp, setSp] = useSearchParams()
  const ref = useReference()
  const org = useQuery({ enabled: me.flags?.org_structure === true, queryKey: ['organisation'], queryFn: () => get<{ units: { id: string; name: string; parentId?: string; closedOn?: string }[] }>('organisation') })
  const navigate = useNavigate()
  const importEnabled = useFlag('project_import')
  const [importing, setImporting] = useState(false)
  const [creating, setCreating] = useState(false)
  const search = useRef<HTMLInputElement>(null)
  const [cols, setCols] = useState<Col[]>(() => JSON.parse(localStorage.getItem('hub.projectCols') ?? 'null') ?? DEFAULT_COLS)
  const filters = Object.fromEntries(sp.entries())
  const mine = sp.get('mine') !== 'false'
  const params = { ...filters, mine, pageSize: 200 }
  const list = useQuery({ queryKey: ['projects', params], queryFn: () => get<P<ProjectRow>>(`projects${qs(params)}`) })
  const set = (k: string, v?: string | null) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); setSp(n, { replace: true }) }
  const toggleCol = (c: Col) => { const n = cols.includes(c) ? cols.filter((x) => x !== c) : [...cols, c]; setCols(n); localStorage.setItem('hub.projectCols', JSON.stringify(n)) }
  const groupBy = sp.get('group') ?? 'status'
  const rows = list.data?.items ?? []
  const groups = useMemo(() => {
    if (groupBy !== 'status') return [{ key: 'all', label: t('projects.all'), rows }]
    const g = [
      { key: 'Active', label: t('projects.group.active'), rows: rows.filter((r) => r.status === 'Active') },
      { key: 'Setup', label: t('projects.group.upcoming'), rows: rows.filter((r) => r.status === 'Setup') },
      { key: 'On Hold', label: t('value.On Hold'), rows: rows.filter((r) => r.status === 'On Hold') },
      { key: 'Closed', label: t('projects.group.closed'), rows: rows.filter((r) => ['Complete', 'Archived', 'Cancelled'].includes(r.status)) },
    ]
    return g.filter((x) => x.rows.length > 0)
  }, [rows, groupBy])
  const sort = sp.get('sort')
  const sortBy = (k: string) => set('sort', sort === `${k}:asc` ? `${k}:desc` : `${k}:asc`)
  const dir = (k: string) => (sort === `${k}:asc` ? 'ascending' : sort === `${k}:desc` ? 'descending' : 'none')
  const ordered = groups.flatMap((g) => g.rows)
  const labelOf = new Map(groups.flatMap((g) => g.rows.map((r) => [r.id, g.label] as const)))
  const columns: ProjectCol[] = [
    { id: 'star', label: t('projects.star'), fixed: true, className: 'w-12 px-2', cell: (r) => <StarButton row={r} onDone={() => list.refetch()} /> },
    { id: 'number', label: t('projects.col.number'), fixed: true, sortKey: 'number', className: 'whitespace-nowrap', cell: (r) => <span className="inline-flex items-center gap-2"><AccentDot id={r.id} /><Key>{r.projectNumber}</Key></span> },
    { id: 'name', label: t('common.name'), fixed: true, sortKey: 'name', className: 'min-w-48',
      cell: (r) => <Link to={href(r)} onClick={(e) => e.stopPropagation()} className="break-words font-semibold hover:underline">{r.name}</Link> },
    { id: 'client', label: t('projects.col.client'), sortKey: 'client', cell: (r) => r.client.name },
    { id: 'pm', label: t('projects.col.pm'), sortKey: 'pm', className: 'whitespace-nowrap', cell: (r) => r.pm.displayName },
    { id: 'office', label: t('projects.col.office'), cell: (r) => r.office ?? <Missing /> },
    { id: 'phase', label: t('projects.col.phase'), cell: (r) => r.phase ?? <Missing /> },
    { id: 'status', label: t('projects.col.status'), sortKey: 'status', cell: (r) => <StatusPill status={r.status} /> },
    { id: 'health', label: t('projects.col.health'), clearsSort: true, cell: (r) => <>
      <Link to={`${href(r)}?why=health`} onClick={(e) => e.stopPropagation()} className="inline-flex rounded-md"><HealthPills r={r} /><span className="sr-only">{t('common.why')}</span></Link>
      <OverrideNote r={r} />
    </> },
    { id: 'priority', label: t('projects.col.priority'), sortKey: 'priority', cell: (r) => <PriorityBadge priority={r.priority} /> },
    { id: 'due', label: t('projects.col.due'), sortKey: 'due', className: 'whitespace-nowrap tabular-nums', cell: (r) => fmtDate(r.targetCompletionDate) },
    { id: 'progress', label: t('projects.col.progress'), sortKey: 'progress', cell: (r) => <ProgressBar pct={r.progressPct} /> },
    { id: 'nextMilestone', label: t('projects.col.nextMilestone'), className: 'min-w-44', cell: (r) => <NextMilestone r={r} /> },
    { id: 'overdue', label: t('projects.col.overdue'), sortKey: 'overdue', className: 'text-right tabular-nums', cell: (r) => <span className={cn(r.overdueTasks > 0 && 'font-semibold text-bad')}>{r.overdueTasks}</span> },
    { id: 'blocked', label: t('projects.col.blocked'), className: 'text-right tabular-nums', cell: (r) => <span className={cn(r.blockedTasks > 0 && 'font-semibold text-bad')}>{r.blockedTasks}</span> },
    { id: 'myRole', label: t('projects.col.myRole'), cell: (r) => r.myRoles.map((x) => runtimeText(`role.${x}`)).join(', ') },
    { id: 'activity', label: t('projects.col.activity'), sortKey: 'activity', className: 'whitespace-nowrap text-xs/[18px] text-muted-foreground', cell: (r) => ago(r.lastActivityAt) },
  ]
  const table = {
    visible: columns.filter((c) => c.fixed || cols.includes(c.id as Col)),
    header: (c: ProjectCol) => c.id === 'star'
      ? <th key={c.id} scope="col" className="w-12 px-2"><span className="sr-only">{c.label}</span></th>
      : <Th key={c.id} label={c.label} numeric={c.className?.includes('text-right')}
          {...(c.sortKey ? { sort: dir(c.sortKey), onSort: () => sortBy(c.sortKey!) } : c.clearsSort ? { onSort: () => set('sort', null) } : {})} />,
    cell: (c: ProjectCol, r: ProjectRow) => <td key={c.id} className={cn(tdCls, c.className)}>{c.cell(r)}</td>,
  }
  const ids = sp.get('ids')
  const tokens = ([
    ['q', t('common.search'), sp.get('q')],
    ['status', t('common.status'), sp.get('status') && runtimeText(`value.${sp.get('status')}`)],
    ['health', t('projects.health'), sp.get('health') && runtimeText(`health.${sp.get('health')}`)],
    ['officeId', t('admin.office'), ref.data?.offices.find((o) => o.id === sp.get('officeId'))?.name],
    ['disciplineId', t('common.discipline'), ref.data?.disciplines.find((d) => d.id === sp.get('disciplineId'))?.name],
    ['leadUnitId', t('org.leadUnit'), org.data?.units.find((u) => u.id === sp.get('leadUnitId'))?.name ?? (sp.get('leadUnitId') ? t('admin.structure.noUnit') : undefined)],
    ['includeArchived', t('projects.includeArchived'), t('common.yes')],
    ['ids', t('projects.filter.chosen'), ids && ids.split(',').filter((x) => x && x !== 'none').length],
    ['computedHealth', t('projects.filter.computedHealth'), sp.get('computedHealth')?.split(',').map((h) => runtimeText(`health.${h}`)).join(', ')],
  ] as const).filter(([k]) => sp.get(k))
  // The search box stays uncontrolled (URL updates arrive in a transition), so removing its filter empties it here.
  const remove = (k: string) => { if (k === 'q' && search.current) search.current.value = ''; set(k, null) }
  const clear = () => { if (search.current) search.current.value = ''; const n = new URLSearchParams(sp); for (const k of FILTERS) n.delete(k); setSp(n, { replace: true }) }
  const create = me.capabilities.createProject && <Button variant="outline" onClick={() => setCreating(true)}><Plus className="size-4" />{t('projects.create')}</Button>

  return (
    <Page title={t('nav.projects')} subtitle={t('projects.subtitle')} actions={<>
      {me.capabilities.admin && importEnabled && <DropdownMenu><DropdownMenuTrigger asChild><Button variant="outline">{t('common.more')}</Button></DropdownMenuTrigger><DropdownMenuContent><DropdownMenuItem onSelect={() => setImporting(true)}>{t('projectImport.title')}</DropdownMenuItem></DropdownMenuContent></DropdownMenu>}
      <ViewMenu listType="projects" />
      <ExportMenu path="projects/export" params={{ ...filters, mine }} name="projects" />
      {me.capabilities.createProject && <Button onClick={() => setCreating(true)}><Plus className="size-4" />{t('projects.create')}</Button>}
    </>}>
      {importing && <ProjectImportDialog onClose={() => setImporting(false)} onChanged={() => list.refetch()} />}
      <FilterBar activeCount={tokens.length}
        search={<Field label={t('common.search')} htmlFor="pl-search">
          <Input ref={search} id="pl-search" type="search" placeholder={t('projects.search')} defaultValue={sp.get('q') ?? ''} onChange={(e) => set('q', e.target.value)} />
        </Field>}
        quick={<Segmented label={t('projects.scope')} value={mine ? 'mine' : 'all'} onChange={(v) => set('mine', v === 'mine' ? null : 'false')}
          options={[{ value: 'mine', label: t('projects.mine') }, { value: 'all', label: t('projects.all') }]} />}
        active={<ActiveFilters tokens={tokens.map(([key, label, value]) => ({ key, label, value: value ?? t('common.dash') }))} onRemove={remove} onClear={clear} />}
        actions={<>
          <ChoiceMenu label={t('common.groupBy')} icon={Rows3} value={groupBy === 'status' ? 'status' : 'none'} onChange={(v) => set('group', v === 'status' ? null : v)}
            options={[{ value: 'status', label: t('common.status') }, { value: 'none', label: t('common.none') }]} />
          <DropdownMenu>
            <DropdownMenuTrigger asChild><Button variant="outline" className="hidden md:inline-flex"><Columns3 className="size-4" />{t('common.columns')}</Button></DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              {COLUMNS.map((c) => <DropdownMenuCheckboxItem key={c} checked={cols.includes(c)} onCheckedChange={() => toggleCol(c)} onSelect={(e) => e.preventDefault()}>{t(`projects.col.${c}`)}</DropdownMenuCheckboxItem>)}
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
      <DataTable table={table} rows={ordered} loading={list.isPending} caption={t('nav.projects')} current={() => false} onRowClick={(r) => navigate(href(r))}
        groupBy={groupBy === 'status' ? (r) => labelOf.get(r.id) ?? '' : undefined}
        groupRow={(label, count, span) => (
          <tr className="border-t">
            <th scope="rowgroup" colSpan={span} className="bg-sidebar px-(--cell-px) py-2 text-left text-xs/[18px] font-semibold uppercase tracking-[0.5px] text-muted-foreground">
              {label} <span className="ml-1 rounded-md bg-card px-2 py-0.5 font-medium normal-case tracking-normal tabular-nums">{count}</span>
            </th>
          </tr>
        )}
        empty={<Empty title={t('projects.emptyTitle')} action={(tokens.length > 0 || mine || create) && (
          <div className="flex flex-wrap justify-center gap-2">
            {tokens.length > 0 && <Button variant="outline" onClick={clear}>{t('filters.clear')}</Button>}
            {mine && <Button variant="outline" onClick={() => set('mine', 'false')}>{t('projects.all')}</Button>}
            {create}
          </div>
        )}>{mine ? t('projects.emptyMine') : t('projects.empty')}</Empty>}
        /* Phones (§13.0): the list becomes cards with the project's identity stripe. */
        cards={<div className="space-y-6 md:hidden">
          {groups.map((g) => (
            <section key={g.key} aria-labelledby={`pl-g-${g.key}`} className="space-y-2">
              <h2 id={`pl-g-${g.key}`} className="text-xs/[18px] font-semibold uppercase tracking-[0.5px] text-muted-foreground">
                {g.label} <span className="ml-1 rounded-md bg-secondary px-2 py-0.5 font-medium normal-case tracking-normal tabular-nums">{g.rows.length}</span>
              </h2>
              <ul className="space-y-2">{g.rows.map((r) => <ProjectCard key={r.id} r={r} onStar={() => list.refetch()} />)}</ul>
            </section>
          ))}
        </div>} />
      {creating && <CreateProjectDialog onClose={() => setCreating(false)} />}
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

function ProjectCard({ r, onStar }: { r: ProjectRow; onStar: () => void }) {
  return (
    <li data-accent={accentOf(r.id)} className="rounded-lg border border-t-4 border-t-(color:--acc-stripe) bg-card p-4 text-sm">
      <div className="flex items-start gap-2">
        <div className="min-w-0 flex-1">
          <Key>{r.projectNumber}</Key>
          <Link to={href(r)} className="block break-words text-base/6 font-semibold hover:underline">{r.name}</Link>
          <p className="text-muted-foreground">{r.client.name} · {r.pm.displayName}</p>
        </div>
        <StarButton row={r} onDone={onStar} />
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
    </li>
  )
}

function StarButton({ row, onDone }: { row: ProjectRow; onDone: () => void }) {
  return (
    <button type="button" aria-label={row.starred ? t('projects.unstar') : t('projects.star')} aria-pressed={row.starred}
      onClick={async (e) => { e.stopPropagation(); await (row.starred ? del(`projects/${row.id}/star`) : post(`projects/${row.id}/star`)); onDone() }}
      className={cn('grid size-(--control-row-h) shrink-0 place-items-center rounded-md hover:bg-secondary', row.starred ? 'text-warn' : 'text-muted-foreground')}>
      <Star className={cn('size-4', row.starred && 'fill-current')} />
    </button>
  )
}
