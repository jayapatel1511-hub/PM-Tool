import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowDown, ArrowUp, Eye, EyeOff, Pencil, RotateCcw } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { Link, useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { Empty, ErrorBanner, Loading, Page } from '@/components/hub/common'
import { useItemPanel } from '@/components/hub/panel-host'
import { Key, StatusPill } from '@/components/hub/pills'
import { scoped, useScope, WorkspaceTabs, type Scope } from '@/components/hub/workspace'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { ApiError, del, get, put, qs } from '@/lib/api'
import { fmtDate, fmtTime, relative } from '@/lib/format'
import { plural, t } from '@/lib/i18n'
import { cn } from '@/lib/utils'

interface Lane { lane: string; statuses: string; count: number }
interface Widget { id: string; hidden: boolean }
interface HomeData {
  today: string; from: string; to: string; asOf: string
  metrics: { activeProjects: number; openTasks: number; overdueTasks: number; onTrack: { green: number; evaluated: number; pct: number | null }; teamMembers: number }
  tasksByStatus: Lane[]
  tasksByProject: { projectId: string; projectNumber: string; name: string; lanes: Lane[]; total: number }[]
  upcomingDeadlines: { total: number; truncated: boolean; items: { type: string; id: string; projectId: string; projectNumber: string; key: string; name: string; date: string; status?: string; owner?: string; overdue: boolean }[] }
  layout: Widget[]; isDefault: boolean
}

/** The same four lanes as the board (§36.3), coloured with text beside every bar. */
const LANE_TONE: Record<string, string> = { 'To Do': 'var(--idle)', 'In Progress': 'var(--work)', Review: 'var(--warn)', Done: 'var(--done)' }
/** Home counts every Complete task as Done; the board's Done lane shows only the last 14 days. */
const laneName = (lane: string) => (lane === 'Done' ? t('home.laneDone') : t(`lane.${lane}`))

/** Project-list links for a scope: its own "mine" default, everything permitted, or the chosen projects. */
function projectsLink(scope: Scope, extra: Record<string, string>) {
  const p = new URLSearchParams({ status: 'Active', ...extra })
  if (scope.kind !== 'mine') p.set('mine', 'false')
  if (scope.ids) p.set('ids', scope.ids.join(',') || 'none')
  return `/projects?${p}`
}

/** Home overview (§36.6, FR-VIS-07): current totals for the selected projects, each opening the list behind it; the
 *  date range changes only Upcoming Deadlines. Each person may reorder or hide the fixed widgets and restore the default. */
export function HomePage() {
  const scope = useScope()
  const [sp, setSp] = useSearchParams()
  const qc = useQueryClient()
  const openPanel = useItemPanel()
  const from = sp.get('from') ?? undefined, to = sp.get('to') ?? undefined
  const key = ['home', scope.api, from, to]
  const q = useQuery({ queryKey: key, enabled: scope.ready, queryFn: () => get<HomeData>(`home${qs({ projects: scope.api, from, to })}`) })
  const [editing, setEditing] = useState(false)
  const set = (k: string, v?: string) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); setSp(n, { replace: true }) }

  const saveLayout = async (widgets: Widget[]) => {
    qc.setQueryData<HomeData>(key, (d) => d && { ...d, layout: widgets, isDefault: false })
    try { await put('me/dashboard-layout', { widgets }) } catch (e) { toast.error((e as ApiError).message) }
    qc.invalidateQueries({ queryKey: ['home'] })
  }
  const restore = async () => {
    const r = await del<{ layout: Widget[] }>('me/dashboard-layout')
    qc.setQueryData<HomeData>(key, (d) => d && { ...d, layout: r.layout, isDefault: true })
    qc.invalidateQueries({ queryKey: ['home'] })
    toast.success(t('home.restored'))
  }

  const d = q.data
  const body = (w: Widget): ReactNode => {
    if (!d) return null
    const m = d.metrics
    switch (w.id) {
      case 'metrics': return (
        <section aria-labelledby="h-metrics">
          <h2 id="h-metrics" className="sr-only">{t('home.metrics')}</h2>
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
            <Tile label={t('home.activeProjects')} value={m.activeProjects} to={projectsLink(scope, {})} />
            <Tile label={t('home.openTasks')} value={m.openTasks} to={scoped(scope, '/tasks', { open: 'true' })} />
            <Tile label={t('home.overdueTasks')} value={m.overdueTasks} to={scoped(scope, '/tasks', { overdue: 'true' })} tone={m.overdueTasks > 0 ? 'text-bad' : undefined} />
            <div className="rounded-lg border bg-card p-3">
              <div className="text-xs text-muted-foreground">{t('home.onTrack')}</div>
              <div className="mt-1 text-2xl font-semibold tabular-nums">{m.onTrack.pct == null ? t('common.dash') : `${m.onTrack.pct}%`}</div>
              <div className="text-xs text-muted-foreground">
                {m.onTrack.evaluated === 0 ? t('home.onTrackNone') : <>
                  <Link className="underline-offset-2 hover:underline" to={projectsLink(scope, { computedHealth: 'Green' })}>{t('home.onTrackGreen', { n: m.onTrack.green })}</Link>
                  {' / '}
                  <Link className="underline-offset-2 hover:underline" to={projectsLink(scope, { computedHealth: 'Green,Yellow,Red' })}>{t('home.onTrackEvaluated', { n: m.onTrack.evaluated })}</Link>
                </>}
              </div>
            </div>
            <Tile label={t('home.teamMembers')} value={m.teamMembers} to={scoped(scope, '/team')} />
          </div>
          <p className="mt-1.5 text-xs text-muted-foreground">{t('home.current', { time: fmtTime(d.asOf) })}</p>
        </section>
      )
      case 'tasksByStatus': {
        const max = Math.max(1, ...d.tasksByStatus.map((l) => l.count))
        return (
          <Card id="h-status" title={t('home.byStatus')} note={t('home.currentShort')}>
            <ul className="space-y-2">
              {d.tasksByStatus.map((l) => (
                <li key={l.lane} className="grid grid-cols-[6.5rem_1fr_2.5rem] items-center gap-2 text-sm">
                  <span>{laneName(l.lane)}</span>
                  <Link to={scoped(scope, '/tasks', { status: l.statuses })} aria-label={t('home.segment', { lane: laneName(l.lane), n: l.count })} className="block h-4 rounded-sm bg-muted hover:ring-2 hover:ring-ring">
                    <span className="block h-full rounded-sm" style={{ width: `${(l.count / max) * 100}%`, background: LANE_TONE[l.lane] }} />
                  </Link>
                  <Link to={scoped(scope, '/tasks', { status: l.statuses })} className="text-right tabular-nums hover:underline">{l.count}</Link>
                </li>
              ))}
            </ul>
          </Card>
        )
      }
      case 'tasksByProject': return <ByProject d={d} scope={scope} />
      case 'upcomingDeadlines': return (
        <Card id="h-deadlines" title={t('home.deadlines')} note={t('home.range', { from: fmtDate(d.from), to: fmtDate(d.to) })} actions={
          <div className="flex flex-wrap items-center gap-2 text-xs">
            <label className="text-muted-foreground">{t('common.from')} <Input type="date" className="inline-flex h-8 w-36" value={from ?? d.from} onChange={(e) => set('from', e.target.value || undefined)} /></label>
            <label className="text-muted-foreground">{t('common.to')} <Input type="date" className="inline-flex h-8 w-36" value={to ?? d.to} onChange={(e) => set('to', e.target.value || undefined)} /></label>
          </div>}>
          {d.upcomingDeadlines.items.length === 0 ? <Empty>{t('home.noDeadlines')}</Empty> : (
            <ul className="-mx-4 divide-y text-sm">
              {d.upcomingDeadlines.items.map((x) => (
                <li key={`${x.type}-${x.id}`}>
                  <button type="button" className="flex w-full flex-wrap items-center gap-x-3 gap-y-0.5 px-4 py-1.5 text-left hover:bg-muted/40" onClick={() => openPanel(x.type, x.id)}>
                    <span className={cn('w-24 shrink-0 tabular-nums', x.overdue && 'font-medium text-bad')}>{fmtDate(x.date)}</span>
                    <span className="w-20 shrink-0 text-xs text-muted-foreground">{t(`itemType.${x.type}`)}</span>
                    <Key>{x.key}</Key><span className="min-w-[8rem] flex-1 truncate">{x.name}</span>
                    <span className="text-xs text-muted-foreground">{x.owner}</span>
                    <span className="text-xs text-muted-foreground">{relative(x.date)}</span>
                    {x.status && <StatusPill status={x.overdue ? 'Overdue' : x.status} />}
                  </button>
                </li>
              ))}
            </ul>
          )}
          {d.upcomingDeadlines.truncated && <p className="mt-2 text-xs text-muted-foreground">{t('home.truncated', { n: d.upcomingDeadlines.items.length })}</p>}
        </Card>
      )
      default: return null
    }
  }

  const layout = d?.layout ?? []
  const shown = editing ? layout : layout.filter((w) => !w.hidden)
  return (
    <Page title={t('nav.home')} subtitle={scope.label} actions={d && (editing ? <>
      {!d.isDefault && <Button variant="outline" size="sm" onClick={restore}><RotateCcw className="size-4" />{t('home.restore')}</Button>}
      <Button size="sm" onClick={() => setEditing(false)}>{t('home.done')}</Button>
    </> : <Button variant="outline" size="sm" onClick={() => setEditing(true)}><Pencil className="size-4" />{t('home.edit')}</Button>)}>
      <WorkspaceTabs />
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      {!d ? <Loading rows={8} /> : (
        <div className="space-y-4">
          {editing && <p className="text-sm text-muted-foreground">{t('home.editHint')}</p>}
          {shown.map((w) => {
            const i = layout.findIndex((x) => x.id === w.id)
            return (
              <div key={w.id} className={cn(editing && 'rounded-lg border border-dashed p-2', editing && w.hidden && 'opacity-50')}>
                {editing && (
                  <div className="mb-2 flex items-center gap-1 text-sm">
                    <span className="flex-1 font-medium">{t(`home.widget.${w.id}`)}{w.hidden && <span className="ml-2 text-xs font-normal text-muted-foreground">{t('home.hidden')}</span>}</span>
                    <Button variant="ghost" size="sm" className="size-8 p-0" disabled={i === 0} aria-label={t('home.moveUp', { name: t(`home.widget.${w.id}`) })}
                      onClick={() => { const n = [...layout]; [n[i - 1], n[i]] = [n[i], n[i - 1]]; saveLayout(n) }}><ArrowUp className="size-4" /></Button>
                    <Button variant="ghost" size="sm" className="size-8 p-0" disabled={i === layout.length - 1} aria-label={t('home.moveDown', { name: t(`home.widget.${w.id}`) })}
                      onClick={() => { const n = [...layout]; [n[i], n[i + 1]] = [n[i + 1], n[i]]; saveLayout(n) }}><ArrowDown className="size-4" /></Button>
                    <Button variant="ghost" size="sm" onClick={() => saveLayout(layout.map((x) => (x.id === w.id ? { ...x, hidden: !x.hidden } : x)))}>
                      {w.hidden ? <><Eye className="size-4" />{t('home.show')}</> : <><EyeOff className="size-4" />{t('home.hide')}</>}
                    </Button>
                  </div>
                )}
                {body(w)}
              </div>
            )
          })}
          {!editing && shown.length === 0 && <div className="rounded-lg border bg-card"><Empty>{t('home.allHidden')}</Empty></div>}
        </div>
      )}
    </Page>
  )
}

function Tile({ label, value, to, tone }: { label: string; value: number; to: string; tone?: string }) {
  return (
    <Link to={to} className="rounded-lg border bg-card p-3 hover:border-ring focus-visible:ring-2">
      <div className="text-xs text-muted-foreground">{label}</div>
      <div className={cn('mt-1 text-2xl font-semibold tabular-nums', tone)}>{value}</div>
    </Link>
  )
}

function Card({ id, title, note, actions, children }: { id: string; title: string; note?: string; actions?: ReactNode; children: ReactNode }) {
  return (
    <section aria-labelledby={id} className="rounded-lg border bg-card p-4">
      <div className="mb-3 flex flex-wrap items-center gap-x-3 gap-y-1">
        <h2 id={id} className="text-sm font-semibold">{title}</h2>
        {note && <span className="text-xs text-muted-foreground">{note}</span>}
        <div className="flex-1" />{actions}
      </div>
      {children}
    </section>
  )
}

/** Tasks by Project: one stacked bar per project, each lane segment opening that project's tasks in the lane. */
function ByProject({ d, scope }: { d: HomeData; scope: Scope }) {
  const [all, setAll] = useState(false)
  const rows = all ? d.tasksByProject : d.tasksByProject.slice(0, 8)
  const max = Math.max(1, ...d.tasksByProject.map((p) => p.total))
  return (
    <Card id="h-projects" title={t('home.byProject')} note={t('home.currentShort')}>
      {rows.length === 0 ? <Empty>{t('home.noTasks')}</Empty> : (
        <ul className="space-y-2">
          {rows.map((p) => (
            <li key={p.projectId} className="grid grid-cols-[minmax(0,12rem)_1fr_2.5rem] items-center gap-2 text-sm">
              <span className="truncate" title={`${p.projectNumber} ${p.name}`}><span className="key font-mono text-xs text-muted-foreground">{p.projectNumber}</span> {p.name}</span>
              <span className="flex h-4 overflow-hidden rounded-sm bg-muted" style={{ width: `${(p.total / max) * 100}%` }}>
                {p.lanes.filter((l) => l.count > 0).map((l) => (
                  <Link key={l.lane} to={scoped(scope, '/tasks', { projectId: p.projectId, status: l.statuses })} className="block h-full hover:opacity-80"
                    style={{ width: `${(l.count / p.total) * 100}%`, background: LANE_TONE[l.lane] }}
                    aria-label={t('home.projectSegment', { project: p.projectNumber, lane: laneName(l.lane), n: l.count })} title={`${laneName(l.lane)}: ${l.count}`} />
                ))}
              </span>
              <Link to={scoped(scope, '/tasks', { projectId: p.projectId, status: p.lanes.map((l) => l.statuses).join(',') })} className="text-right tabular-nums hover:underline">{p.total}</Link>
            </li>
          ))}
        </ul>
      )}
      <div className="mt-3 flex flex-wrap items-center gap-3 text-xs text-muted-foreground">
        {Object.entries(LANE_TONE).map(([lane, c]) => <span key={lane} className="flex items-center gap-1"><span className="inline-block size-2.5 rounded-sm" style={{ background: c }} aria-hidden />{laneName(lane)}</span>)}
        {d.tasksByProject.length > 8 && <Button variant="ghost" size="sm" className="ml-auto h-7" onClick={() => setAll(!all)}>{all ? t('home.fewer') : plural(d.tasksByProject.length, 'home.allOne', 'home.allMany', { n: d.tasksByProject.length })}</Button>}
      </div>
    </Card>
  )
}
