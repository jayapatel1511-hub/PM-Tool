import { useQuery } from '@tanstack/react-query'
import { Columns3, Plus, Star } from 'lucide-react'
import { useMemo, useRef, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { AccentDot, ActiveFilters, Empty, ErrorBanner, Field, FilterBar, Loading, Missing, Page, Segmented, TableRegion, selectCls, tdCls, thCls } from '@/components/hub/common'
import { Chip, HealthPill, Key, PriorityBadge, ProgressBar, StatusPill } from '@/components/hub/pills'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { DropdownMenu, DropdownMenuCheckboxItem, DropdownMenuContent, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { useReference } from '@/hooks/data'
import { del, get, post, qs } from '@/lib/api'
import { useMe } from '@/lib/auth'
import { ago, fmtDate, relative } from '@/lib/format'
import { t } from '@/lib/i18n'
import type { Page as P, ProjectRow } from '@/lib/types'
import { accentOf, cn } from '@/lib/utils'
import { CreateProjectDialog } from './CreateProject'
import { ExportMenu } from '@/components/hub/export'
import { ViewMenu } from '@/components/hub/views'

const COLUMNS = ['client', 'pm', 'office', 'phase', 'status', 'health', 'priority', 'due', 'progress', 'nextMilestone', 'overdue', 'blocked', 'myRole', 'activity'] as const
type Col = (typeof COLUMNS)[number]
const DEFAULT_COLS: Col[] = ['client', 'pm', 'status', 'health', 'priority', 'due', 'progress', 'nextMilestone', 'overdue', 'blocked', 'myRole']
/** URL filters shown as removable tokens; scope (mine), grouping and sort are view choices, not filters. */
const FILTERS = ['q', 'status', 'health', 'officeId', 'disciplineId', 'includeArchived', 'ids', 'computedHealth'] as const
const href = (r: ProjectRow) => `/projects/${encodeURIComponent(r.projectNumber)}`

/** Project list and grouped board presentation (§13.2, FR-021, §36.2 FR-VIS-03). Filters live in the URL. */
export function ProjectListPage() {
  const me = useMe()
  const [sp, setSp] = useSearchParams()
  const ref = useReference()
  const navigate = useNavigate()
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
  const shown = COLUMNS.filter((c) => cols.includes(c))
  const ids = sp.get('ids')
  const tokens = ([
    ['q', t('common.search'), sp.get('q')],
    ['status', t('common.status'), sp.get('status') && t(`value.${sp.get('status')}`)],
    ['health', t('projects.health'), sp.get('health') && t(`health.${sp.get('health')}`)],
    ['officeId', t('admin.office'), ref.data?.offices.find((o) => o.id === sp.get('officeId'))?.name],
    ['disciplineId', t('common.discipline'), ref.data?.disciplines.find((d) => d.id === sp.get('disciplineId'))?.name],
    ['includeArchived', t('projects.includeArchived'), t('common.yes')],
    ['ids', t('projects.filter.chosen'), ids && ids.split(',').filter((x) => x && x !== 'none').length],
    ['computedHealth', t('projects.filter.computedHealth'), sp.get('computedHealth')?.split(',').map((h) => t(`health.${h}`)).join(', ')],
  ] as const).filter(([k]) => sp.get(k))
  // The search box stays uncontrolled (URL updates arrive in a transition), so removing its filter empties it here.
  const remove = (k: string) => { if (k === 'q' && search.current) search.current.value = ''; set(k, null) }
  const clear = () => { if (search.current) search.current.value = ''; const n = new URLSearchParams(sp); for (const k of FILTERS) n.delete(k); setSp(n, { replace: true }) }
  const create = me.capabilities.createProject && <Button variant="outline" onClick={() => setCreating(true)}><Plus className="size-4" />{t('projects.create')}</Button>

  return (
    <Page title={t('nav.projects')} subtitle={t('projects.subtitle')} actions={<>
      <ViewMenu listType="projects" />
      <ExportMenu path="projects/export" params={{ ...filters, mine }} name="projects" />
      {me.capabilities.createProject && <Button onClick={() => setCreating(true)}><Plus className="size-4" />{t('projects.create')}</Button>}
    </>}>
      <FilterBar>
        <div className="flex flex-wrap items-end gap-3">
          <Segmented label={t('projects.scope')} value={mine ? 'mine' : 'all'} onChange={(v) => set('mine', v === 'mine' ? null : 'false')}
            options={[{ value: 'mine', label: t('projects.mine') }, { value: 'all', label: t('projects.all') }]} />
          <Field label={t('common.search')} htmlFor="pl-search" className="w-full sm:w-72">
            <Input ref={search} id="pl-search" type="search" placeholder={t('projects.search')} defaultValue={sp.get('q') ?? ''} onChange={(e) => set('q', e.target.value)} />
          </Field>
          <Field label={t('common.status')} htmlFor="pl-status" className="w-full sm:w-48">
            <select id="pl-status" className={selectCls} value={sp.get('status') ?? ''} onChange={(e) => set('status', e.target.value)}>
              <option value="">{t('projects.liveStatuses')}</option>
              {['Setup', 'Active', 'On Hold', 'Complete', 'Archived', 'Cancelled'].map((s) => <option key={s} value={s}>{t(`value.${s}`)}</option>)}
            </select>
          </Field>
          <Field label={t('projects.health')} htmlFor="pl-health" className="w-full sm:w-40">
            <select id="pl-health" className={selectCls} value={sp.get('health') ?? ''} onChange={(e) => set('health', e.target.value)}>
              <option value="">{t('projects.anyHealth')}</option>
              {['Red', 'Yellow', 'Green', 'Grey'].map((h) => <option key={h} value={h}>{t(`health.${h}`)}</option>)}
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
          <label className="flex min-h-(--control-h) items-center gap-2 text-sm">
            <Checkbox checked={sp.get('includeArchived') === 'true'} onCheckedChange={(c) => set('includeArchived', c ? 'true' : null)} />{t('projects.includeArchived')}
          </label>
        </div>
        <ActiveFilters tokens={tokens.map(([key, label, value]) => ({ key, label, value: value ?? t('common.dash') }))} onRemove={remove} onClear={clear} />
      </FilterBar>

      <div className="flex flex-wrap items-center justify-end gap-3">
        <div className="flex items-center gap-2">
          <label htmlFor="pl-group" className="text-sm font-medium">{t('common.groupBy')}</label>
          <select id="pl-group" className={cn(selectCls, 'w-auto')} value={groupBy} onChange={(e) => set('group', e.target.value === 'status' ? null : e.target.value)}>
            <option value="status">{t('projects.groupStatus')}</option>
            <option value="none">{t('common.noGroup')}</option>
          </select>
        </div>
        <DropdownMenu>
          <DropdownMenuTrigger asChild><Button variant="outline" className="hidden md:inline-flex"><Columns3 className="size-4" />{t('common.columns')}</Button></DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            {COLUMNS.map((c) => <DropdownMenuCheckboxItem key={c} checked={cols.includes(c)} onCheckedChange={() => toggleCol(c)} onSelect={(e) => e.preventDefault()}>{t(`projects.col.${c}`)}</DropdownMenuCheckboxItem>)}
          </DropdownMenuContent>
        </DropdownMenu>
      </div>

      {list.error && <ErrorBanner error={list.error} retry={() => list.refetch()} />}
      {list.isPending ? <div className="rounded-lg border bg-card"><Loading rows={8} /></div> : rows.length === 0 ? (
        <div className="rounded-lg border bg-card">
          <Empty title={t('projects.emptyTitle')} action={(tokens.length > 0 || mine || create) && (
            <div className="flex flex-wrap justify-center gap-2">
              {tokens.length > 0 && <Button variant="outline" onClick={clear}>{t('filters.clear')}</Button>}
              {mine && <Button variant="outline" onClick={() => set('mine', 'false')}>{t('projects.all')}</Button>}
              {create}
            </div>
          )}>{mine ? t('projects.emptyMine') : t('projects.empty')}</Empty>
        </div>
      ) : (
        <>
          {/* Phones (§13.0): the list becomes cards with the project's identity stripe. */}
          <div className="space-y-6 md:hidden">
            {groups.map((g) => (
              <section key={g.key} aria-labelledby={`pl-g-${g.key}`} className="space-y-2">
                <h2 id={`pl-g-${g.key}`} className="text-xs/[18px] font-semibold uppercase tracking-[0.5px] text-muted-foreground">
                  {g.label} <span className="ml-1 rounded-md bg-secondary px-2 py-0.5 font-medium normal-case tracking-normal tabular-nums">{g.rows.length}</span>
                </h2>
                <ul className="space-y-2">{g.rows.map((r) => <ProjectCard key={r.id} r={r} onStar={() => list.refetch()} />)}</ul>
              </section>
            ))}
          </div>
          <TableRegion className="hidden md:block">
            <table className="w-full text-sm">
              <caption className="sr-only">{t('nav.projects')}</caption>
              <thead className="bg-muted">
                <tr>
                  <th scope="col" className="w-12 px-2"><span className="sr-only">{t('projects.star')}</span></th>
                  <Th label={t('projects.col.number')} sort={dir('number')} onSort={() => sortBy('number')} />
                  <Th label={t('common.name')} sort={dir('name')} onSort={() => sortBy('name')} />
                  {shown.includes('client') && <Th label={t('projects.col.client')} sort={dir('client')} onSort={() => sortBy('client')} />}
                  {shown.includes('pm') && <Th label={t('projects.col.pm')} sort={dir('pm')} onSort={() => sortBy('pm')} />}
                  {shown.includes('office') && <Th label={t('projects.col.office')} />}
                  {shown.includes('phase') && <Th label={t('projects.col.phase')} />}
                  {shown.includes('status') && <Th label={t('projects.col.status')} sort={dir('status')} onSort={() => sortBy('status')} />}
                  {shown.includes('health') && <Th label={t('projects.col.health')} onSort={() => set('sort', null)} />}
                  {shown.includes('priority') && <Th label={t('projects.col.priority')} sort={dir('priority')} onSort={() => sortBy('priority')} />}
                  {shown.includes('due') && <Th label={t('projects.col.due')} sort={dir('due')} onSort={() => sortBy('due')} />}
                  {shown.includes('progress') && <Th label={t('projects.col.progress')} sort={dir('progress')} onSort={() => sortBy('progress')} />}
                  {shown.includes('nextMilestone') && <Th label={t('projects.col.nextMilestone')} />}
                  {shown.includes('overdue') && <Th label={t('projects.col.overdue')} sort={dir('overdue')} onSort={() => sortBy('overdue')} numeric />}
                  {shown.includes('blocked') && <Th label={t('projects.col.blocked')} numeric />}
                  {shown.includes('myRole') && <Th label={t('projects.col.myRole')} />}
                  {shown.includes('activity') && <Th label={t('projects.col.activity')} sort={dir('activity')} onSort={() => sortBy('activity')} />}
                </tr>
              </thead>
              {groups.map((g) => (
                <tbody key={g.key}>
                  {groupBy === 'status' && (
                    <tr className="border-t">
                      <th scope="rowgroup" colSpan={3 + shown.length} className="bg-sidebar px-(--cell-px) py-2 text-left text-xs/[18px] font-semibold uppercase tracking-[0.5px] text-muted-foreground">
                        {g.label} <span className="ml-1 rounded-md bg-card px-2 py-0.5 font-medium normal-case tracking-normal tabular-nums">{g.rows.length}</span>
                      </th>
                    </tr>
                  )}
                  {g.rows.map((r) => (
                    <tr key={r.id} className="cursor-pointer border-t hover:bg-muted" onClick={() => navigate(href(r))}>
                      <td className="w-12 px-2 py-(--cell-py) align-top"><StarButton row={r} onDone={() => list.refetch()} /></td>
                      <td className={cn(tdCls, 'whitespace-nowrap')}><span className="inline-flex items-center gap-2"><AccentDot id={r.id} /><Key>{r.projectNumber}</Key></span></td>
                      <td className={cn(tdCls, 'min-w-48')}><Link to={href(r)} onClick={(e) => e.stopPropagation()} className="break-words font-semibold hover:underline">{r.name}</Link></td>
                      {shown.includes('client') && <td className={tdCls}>{r.client.name}</td>}
                      {shown.includes('pm') && <td className={cn(tdCls, 'whitespace-nowrap')}>{r.pm.displayName}</td>}
                      {shown.includes('office') && <td className={tdCls}>{r.office ?? <Missing />}</td>}
                      {shown.includes('phase') && <td className={tdCls}>{r.phase ?? <Missing />}</td>}
                      {shown.includes('status') && <td className={tdCls}><StatusPill status={r.status} /></td>}
                      {shown.includes('health') && (
                        <td className={tdCls}>
                          <Link to={`${href(r)}?why=health`} onClick={(e) => e.stopPropagation()} className="inline-flex rounded-md">
                            <HealthPills r={r} /><span className="sr-only">{t('common.why')}</span>
                          </Link>
                          <OverrideNote r={r} />
                        </td>
                      )}
                      {shown.includes('priority') && <td className={tdCls}><PriorityBadge priority={r.priority} /></td>}
                      {shown.includes('due') && <td className={cn(tdCls, 'whitespace-nowrap tabular-nums')}>{fmtDate(r.targetCompletionDate)}</td>}
                      {shown.includes('progress') && <td className={tdCls}><ProgressBar pct={r.progressPct} /></td>}
                      {shown.includes('nextMilestone') && <td className={cn(tdCls, 'min-w-44')}><NextMilestone r={r} /></td>}
                      {shown.includes('overdue') && <td className={cn(tdCls, 'text-right tabular-nums', r.overdueTasks > 0 && 'font-semibold text-bad')}>{r.overdueTasks}</td>}
                      {shown.includes('blocked') && <td className={cn(tdCls, 'text-right tabular-nums', r.blockedTasks > 0 && 'font-semibold text-bad')}>{r.blockedTasks}</td>}
                      {shown.includes('myRole') && <td className={tdCls}>{r.myRoles.map((x) => t(`role.${x}`)).join(', ')}</td>}
                      {shown.includes('activity') && <td className={cn(tdCls, 'whitespace-nowrap text-xs/[18px] text-muted-foreground')}>{ago(r.lastActivityAt)}</td>}
                    </tr>
                  ))}
                </tbody>
              ))}
            </table>
          </TableRegion>
        </>
      )}
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
