import { useQuery } from '@tanstack/react-query'
import { Columns3, Plus, Star, X } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { Empty, ErrorBanner, Loading, Page } from '@/components/hub/common'
import { HealthPill, Key, PriorityBadge, ProgressBar, StatusPill } from '@/components/hub/pills'
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
import { cn } from '@/lib/utils'
import { CreateProjectDialog } from './CreateProject'
import { ExportMenu } from '@/components/hub/export'
import { ViewMenu } from '@/components/hub/views'

const COLUMNS = ['client', 'pm', 'office', 'phase', 'status', 'health', 'priority', 'due', 'progress', 'nextMilestone', 'overdue', 'blocked', 'myRole', 'activity'] as const
type Col = (typeof COLUMNS)[number]
const DEFAULT_COLS: Col[] = ['client', 'pm', 'status', 'health', 'priority', 'due', 'progress', 'nextMilestone', 'overdue', 'blocked', 'myRole']

/** Project list and grouped board presentation (§13.2, FR-021, §36.2 FR-VIS-03). Filters live in the URL. */
export function ProjectListPage() {
  const me = useMe()
  const [sp, setSp] = useSearchParams()
  const ref = useReference()
  const navigate = useNavigate()
  const [creating, setCreating] = useState(false)
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
  const sortBy = (k: string) => set('sort', sp.get('sort') === `${k}:asc` ? `${k}:desc` : `${k}:asc`)

  return (
    <Page title={t('nav.projects')} actions={<>
      <ViewMenu listType="projects" />
      <ExportMenu path="projects/export" params={{ ...filters, mine }} name="projects" />
      {me.capabilities.createProject && <Button onClick={() => setCreating(true)}><Plus className="size-4" />{t('projects.create')}</Button>}
    </>}>
      <div className="flex flex-wrap items-center gap-2">
        <Input className="h-8 w-64" placeholder={t('projects.search')} defaultValue={sp.get('q') ?? ''} onChange={(e) => set('q', e.target.value)} aria-label={t('projects.search')} />
        <div className="inline-flex rounded-md border bg-card p-0.5" role="group" aria-label={t('projects.scope')}>
          {[true, false].map((m) => (
            <button key={String(m)} className={cn('rounded px-2.5 py-1 text-sm', mine === m && 'bg-accent font-medium text-accent-foreground')} aria-pressed={mine === m}
              onClick={() => set('mine', m ? null : 'false')}>{m ? t('projects.mine') : t('projects.all')}</button>
          ))}
        </div>
        <select className="h-8 rounded-md border bg-card px-2 text-sm" value={sp.get('status') ?? ''} onChange={(e) => set('status', e.target.value)} aria-label={t('common.status')}>
          <option value="">{t('projects.liveStatuses')}</option>
          {['Setup', 'Active', 'On Hold', 'Complete', 'Archived', 'Cancelled'].map((s) => <option key={s} value={s}>{t(`value.${s}`)}</option>)}
        </select>
        <select className="h-8 rounded-md border bg-card px-2 text-sm" value={sp.get('health') ?? ''} onChange={(e) => set('health', e.target.value)} aria-label={t('projects.health')}>
          <option value="">{t('projects.anyHealth')}</option>
          {['Red', 'Yellow', 'Green', 'Grey'].map((h) => <option key={h} value={h}>{t(`health.${h}`)}</option>)}
        </select>
        <select className="h-8 rounded-md border bg-card px-2 text-sm" value={sp.get('officeId') ?? ''} onChange={(e) => set('officeId', e.target.value)} aria-label={t('admin.office')}>
          <option value="">{t('projects.anyOffice')}</option>
          {ref.data?.offices.map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
        </select>
        <select className="h-8 rounded-md border bg-card px-2 text-sm" value={sp.get('disciplineId') ?? ''} onChange={(e) => set('disciplineId', e.target.value)} aria-label={t('common.discipline')}>
          <option value="">{t('projects.anyDiscipline')}</option>
          {ref.data?.disciplines.filter((d) => d.isActive).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
        </select>
        <label className="flex items-center gap-2 text-sm"><Checkbox checked={sp.get('includeArchived') === 'true'} onCheckedChange={(c) => set('includeArchived', c ? 'true' : null)} />{t('projects.includeArchived')}</label>
        <div className="flex-1" />
        <select className="h-8 rounded-md border bg-card px-2 text-sm" value={groupBy} onChange={(e) => set('group', e.target.value === 'status' ? null : e.target.value)} aria-label={t('common.groupBy')}>
          <option value="status">{t('projects.groupStatus')}</option>
          <option value="none">{t('common.noGroup')}</option>
        </select>
        <DropdownMenu>
          <DropdownMenuTrigger asChild><Button variant="outline" size="sm"><Columns3 className="size-4" />{t('common.columns')}</Button></DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            {COLUMNS.map((c) => <DropdownMenuCheckboxItem key={c} checked={cols.includes(c)} onCheckedChange={() => toggleCol(c)} onSelect={(e) => e.preventDefault()}>{t(`projects.col.${c}`)}</DropdownMenuCheckboxItem>)}
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
      {(sp.get('ids') || sp.get('computedHealth')) && (
        <div className="flex flex-wrap items-center gap-1.5 text-xs" role="status">
          {sp.get('ids') && <span className="inline-flex items-center gap-1 rounded-full border bg-muted px-2 py-0.5">{t('projects.chosenIds', { n: sp.get('ids')!.split(',').filter((x) => x && x !== 'none').length })}
            <button type="button" aria-label={t('task.removeFilter', { name: t('projects.chosenIds', { n: '' }) })} onClick={() => set('ids', null)}><X className="size-3" /></button></span>}
          {sp.get('computedHealth') && <span className="inline-flex items-center gap-1 rounded-full border bg-muted px-2 py-0.5">{t('projects.computedHealth', { list: sp.get('computedHealth')!.split(',').map((h) => t(`health.${h}`)).join(', ') })}
            <button type="button" aria-label={t('task.removeFilter', { name: t('projects.computedHealth', { list: '' }) })} onClick={() => set('computedHealth', null)}><X className="size-3" /></button></span>}
        </div>
      )}
      {list.error && <ErrorBanner error={list.error} retry={() => list.refetch()} />}
      {list.isPending ? <Loading rows={8} /> : rows.length === 0 ? (
        <div className="rounded-lg border bg-card"><Empty action={me.capabilities.createProject && <Button onClick={() => setCreating(true)}>{t('projects.create')}</Button>}>{mine ? t('projects.emptyMine') : t('projects.empty')}</Empty></div>
      ) : (
        <div className="overflow-x-auto rounded-lg border bg-card">
          <table className="w-full text-[13px]">
            <thead className="sticky top-0 bg-muted/80 text-left text-xs text-muted-foreground backdrop-blur">
              <tr>
                <th className="w-8 px-2 py-2"><span className="sr-only">{t('projects.star')}</span></th>
                <Th label={t('projects.col.number')} onSort={() => sortBy('number')} />
                <Th label={t('common.name')} onSort={() => sortBy('name')} />
                {cols.includes('client') && <Th label={t('projects.col.client')} onSort={() => sortBy('client')} />}
                {cols.includes('pm') && <Th label={t('projects.col.pm')} onSort={() => sortBy('pm')} />}
                {cols.includes('office') && <Th label={t('projects.col.office')} />}
                {cols.includes('phase') && <Th label={t('projects.col.phase')} />}
                {cols.includes('status') && <Th label={t('projects.col.status')} onSort={() => sortBy('status')} />}
                {cols.includes('health') && <Th label={t('projects.col.health')} onSort={() => set('sort', null)} />}
                {cols.includes('priority') && <Th label={t('projects.col.priority')} onSort={() => sortBy('priority')} />}
                {cols.includes('due') && <Th label={t('projects.col.due')} onSort={() => sortBy('due')} />}
                {cols.includes('progress') && <Th label={t('projects.col.progress')} onSort={() => sortBy('progress')} />}
                {cols.includes('nextMilestone') && <Th label={t('projects.col.nextMilestone')} />}
                {cols.includes('overdue') && <Th label={t('projects.col.overdue')} onSort={() => sortBy('overdue')} />}
                {cols.includes('blocked') && <Th label={t('projects.col.blocked')} />}
                {cols.includes('myRole') && <Th label={t('projects.col.myRole')} />}
                {cols.includes('activity') && <Th label={t('projects.col.activity')} onSort={() => sortBy('activity')} />}
              </tr>
            </thead>
            {groups.map((g) => (
              <tbody key={g.key}>
                {groupBy === 'status' && (
                  <tr className="border-t bg-muted/30"><th colSpan={20} className="px-3 py-1.5 text-left text-sm font-semibold text-primary">{g.label} <span className="ml-1 rounded-full bg-card px-2 text-xs text-muted-foreground">{g.rows.length}</span></th></tr>
                )}
                {g.rows.map((r) => (
                  <tr key={r.id} className="cursor-pointer border-t hover:bg-muted/40" onClick={() => navigate(`/projects/${encodeURIComponent(r.projectNumber)}`)}>
                    <td className="px-2 py-1.5" onClick={(e) => e.stopPropagation()}><StarButton row={r} onDone={() => list.refetch()} /></td>
                    <td className="whitespace-nowrap px-3 py-1.5"><Key>{r.projectNumber}</Key></td>
                    <td className="px-3 py-1.5 font-medium"><Link to={`/projects/${encodeURIComponent(r.projectNumber)}`} onClick={(e) => e.stopPropagation()} className="hover:underline">{r.name}</Link></td>
                    {cols.includes('client') && <td className="px-3 py-1.5">{r.client.name}</td>}
                    {cols.includes('pm') && <td className="whitespace-nowrap px-3 py-1.5">{r.pm.displayName}</td>}
                    {cols.includes('office') && <td className="px-3 py-1.5">{r.office}</td>}
                    {cols.includes('phase') && <td className="px-3 py-1.5">{r.phase ?? t('common.dash')}</td>}
                    {cols.includes('status') && <td className="px-3 py-1.5"><StatusPill status={r.status} /></td>}
                    {cols.includes('health') && (
                      <td className="px-3 py-1.5" onClick={(e) => { e.stopPropagation(); navigate(`/projects/${encodeURIComponent(r.projectNumber)}?why=health`) }}>
                        <div className="flex flex-wrap gap-1">
                          <HealthPill health={r.reportedHealth} />
                          {r.reportedHealth !== r.computedHealth && <HealthPill health={r.computedHealth} label={t('health.computed')} />}
                        </div>
                      </td>
                    )}
                    {cols.includes('priority') && <td className="px-3 py-1.5"><PriorityBadge priority={r.priority} /></td>}
                    {cols.includes('due') && <td className="whitespace-nowrap px-3 py-1.5">{fmtDate(r.targetCompletionDate)}</td>}
                    {cols.includes('progress') && <td className="px-3 py-1.5"><ProgressBar pct={r.progressPct} /></td>}
                    {cols.includes('nextMilestone') && <td className="px-3 py-1.5">{r.nextMilestone ? <span className="whitespace-nowrap">{r.nextMilestone.name} <span className="text-muted-foreground">· {fmtDate(r.nextMilestone.date)} ({relative(r.nextMilestone.date)})</span></span> : t('common.dash')}</td>}
                    {cols.includes('overdue') && <td className={cn('px-3 py-1.5 tabular-nums', r.overdueTasks > 0 && 'font-semibold text-bad')}>{r.overdueTasks}</td>}
                    {cols.includes('blocked') && <td className={cn('px-3 py-1.5 tabular-nums', r.blockedTasks > 0 && 'font-semibold text-bad')}>{r.blockedTasks}</td>}
                    {cols.includes('myRole') && <td className="px-3 py-1.5 text-xs">{r.myRoles.map((x) => t(`role.${x}`)).join(', ')}</td>}
                    {cols.includes('activity') && <td className="whitespace-nowrap px-3 py-1.5 text-xs text-muted-foreground">{ago(r.lastActivityAt)}</td>}
                  </tr>
                ))}
              </tbody>
            ))}
          </table>
        </div>
      )}
      {creating && <CreateProjectDialog onClose={() => setCreating(false)} />}
    </Page>
  )
}

function Th({ label, onSort }: { label: string; onSort?: () => void }) {
  return <th className="whitespace-nowrap px-3 py-2 font-medium">{onSort ? <button className="hover:text-foreground" onClick={onSort}>{label}</button> : label}</th>
}

function StarButton({ row, onDone }: { row: ProjectRow; onDone: () => void }) {
  return (
    <button aria-label={row.starred ? t('projects.unstar') : t('projects.star')} aria-pressed={row.starred}
      onClick={async () => { await (row.starred ? del(`projects/${row.id}/star`) : post(`projects/${row.id}/star`)); onDone() }}
      className={cn('rounded p-0.5 hover:bg-muted', row.starred ? 'text-warn' : 'text-muted-foreground/50')}>
      <Star className={cn('size-3.5', row.starred && 'fill-current')} />
    </button>
  )
}
