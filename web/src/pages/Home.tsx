import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowDown, ArrowUp, Eye, EyeOff, Pencil, RotateCcw } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { Link, useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { AccentDot, Empty, ErrorBanner, Loading, Missing, Notice, Page, Section, SummaryTile } from '@/components/hub/common'
import { useItemPanel } from '@/components/hub/panel-host'
import { Chip, Key, StatusPill } from '@/components/hub/pills'
import { scoped, useScope, WorkspaceTabs, type Scope } from '@/components/hub/workspace'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { ApiError, del, get, put, qs } from '@/lib/api'
import { useMe } from '@/lib/auth'
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
const note = (text: string) => <span className="text-xs/[18px] text-muted-foreground">{text}</span>

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
  const me = useMe()
  const scope = useScope()
  const [sp, setSp] = useSearchParams()
  const qc = useQueryClient()
  const openPanel = useItemPanel()
  const from = sp.get('from') ?? undefined, to = sp.get('to') ?? undefined
  const key = ['home', scope.api, from, to]
  const q = useQuery({ queryKey: key, enabled: scope.ready, queryFn: () => get<HomeData>(`home${qs({ projects: scope.api, from, to })}`) })
  const [editing, setEditing] = useState(false)
  const set = (k: string, v?: string) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); setSp(n, { replace: true }) }
  const clearRange = () => { const n = new URLSearchParams(sp); n.delete('from'); n.delete('to'); setSp(n, { replace: true }) }

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
      // Tiles in the Home order (blue, mint, lavender, peach); a warning travels as a labelled chip, never as the tint.
      case 'metrics': return (
        <section aria-labelledby="h-metrics">
          <h2 id="h-metrics" className="sr-only">{t('home.metrics')}</h2>
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 sm:gap-4 xl:grid-cols-5">
            <SummaryTile label={t('home.activeProjects')} value={m.activeProjects} accent="blue" to={projectsLink(scope, {})} />
            <SummaryTile label={t('home.openTasks')} value={m.openTasks} accent="mint" to={scoped(scope, '/tasks', { open: 'true' })} />
            <SummaryTile label={t('home.overdueTasks')} value={m.overdueTasks} accent="lavender" to={scoped(scope, '/tasks', { overdue: 'true' })}>
              {m.overdueTasks > 0 && <span className="mt-1.5 block"><Chip tone="bad">{t('dash.attention')}</Chip></span>}
            </SummaryTile>
            {/* Two destinations (numerator and denominator), so the tile itself is not a link. */}
            <SummaryTile label={t('home.onTrack')} value={m.onTrack.pct == null ? <Missing /> : `${m.onTrack.pct}%`} accent="peach">
              <span className="mt-0.5 block text-xs/[18px] text-muted-foreground tabular-nums">
                {m.onTrack.evaluated === 0 ? t('home.onTrackNone') : <>
                  <Link className="text-primary underline underline-offset-4 hover:text-foreground" to={projectsLink(scope, { computedHealth: 'Green' })}>{t('home.onTrackGreen', { n: m.onTrack.green })}</Link>
                  {' / '}
                  <Link className="text-primary underline underline-offset-4 hover:text-foreground" to={projectsLink(scope, { computedHealth: 'Green,Yellow,Red' })}>{t('home.onTrackEvaluated', { n: m.onTrack.evaluated })}</Link>
                </>}
              </span>
            </SummaryTile>
            <SummaryTile label={t('home.teamMembers')} value={m.teamMembers} accent="rose" to={scoped(scope, '/team')} />
          </div>
          <p className="mt-2 text-xs/[18px] text-muted-foreground">{t('home.current', { time: fmtTime(d.asOf) })}</p>
        </section>
      )
      case 'tasksByStatus': {
        const max = Math.max(1, ...d.tasksByStatus.map((l) => l.count))
        return (
          <Section id="h-status" accent="blue" title={t('home.byStatus')} actions={note(t('home.currentShort'))}>
            <ul className="space-y-2 p-5">
              {d.tasksByStatus.map((l) => (
                <li key={l.lane} className="grid grid-cols-[7rem_1fr_3rem] items-center gap-3 text-sm">
                  <span>{laneName(l.lane)}</span>
                  <Link to={scoped(scope, '/tasks', { status: l.statuses })} aria-label={t('home.segment', { lane: laneName(l.lane), n: l.count })} className="block h-4 rounded-full bg-secondary hover:ring-2 hover:ring-ring">
                    <span className="block h-full rounded-full" style={{ width: `${(l.count / max) * 100}%`, background: LANE_TONE[l.lane] }} />
                  </Link>
                  <Link to={scoped(scope, '/tasks', { status: l.statuses })} className="text-right font-medium tabular-nums hover:underline">{l.count}</Link>
                </li>
              ))}
            </ul>
          </Section>
        )
      }
      case 'tasksByProject': return <ByProject d={d} scope={scope} />
      case 'upcomingDeadlines': return (
        <Section id="h-deadlines" accent="peach" title={t('home.deadlines')} actions={
          <div className="flex flex-wrap items-center gap-x-3 gap-y-2">
            {note(t('home.range', { from: fmtDate(d.from), to: fmtDate(d.to) }))}
            <label className="flex items-center gap-2 text-sm text-muted-foreground">{t('common.from')}<Input type="date" className="w-40" value={from ?? d.from} onChange={(e) => set('from', e.target.value || undefined)} /></label>
            <label className="flex items-center gap-2 text-sm text-muted-foreground">{t('common.to')}<Input type="date" className="w-40" value={to ?? d.to} onChange={(e) => set('to', e.target.value || undefined)} /></label>
          </div>}>
          {d.upcomingDeadlines.items.length === 0
            ? <Empty action={(from || to) && <Button variant="outline" onClick={clearRange}>{t('filters.clear')}</Button>}>{t('home.noDeadlines')}</Empty> : (
            <ul className="divide-y">
              {d.upcomingDeadlines.items.map((x) => (
                <li key={`${x.type}-${x.id}`}>
                  <button type="button" className="flex min-h-(--row-min) w-full flex-wrap items-center gap-x-3 gap-y-1 px-5 py-(--cell-py) text-left text-sm hover:bg-muted" onClick={() => openPanel(x.type, x.id)}>
                    <span className={cn('w-24 shrink-0 tabular-nums', x.overdue && 'font-semibold text-bad')}>{fmtDate(x.date)}</span>
                    <span className="w-24 shrink-0 text-xs/[18px] text-muted-foreground">{t(`itemType.${x.type}`)}</span>
                    <span className="flex min-w-[10rem] flex-1 items-center gap-2"><AccentDot id={x.projectId} /><Key>{x.key}</Key><span className="min-w-0 truncate font-medium">{x.name}</span></span>
                    {x.owner && <span className="text-xs/[18px] text-muted-foreground">{x.owner}</span>}
                    <span className="text-xs/[18px] text-muted-foreground">{relative(x.date)}</span>
                    {/* Overdue never travels by colour alone (§13.0). */}
                    {(x.status || x.overdue) && <StatusPill status={x.overdue ? 'Overdue' : x.status} />}
                  </button>
                </li>
              ))}
            </ul>
          )}
          {d.upcomingDeadlines.truncated && <p className="border-t px-5 py-2.5 text-xs/[18px] text-muted-foreground">{t('home.truncated', { n: d.upcomingDeadlines.items.length })}</p>}
        </Section>
      )
      default: return null
    }
  }

  const layout = d?.layout ?? []
  const shown = editing ? layout : layout.filter((w) => !w.hidden)
  return (
    <Page eyebrow={scope.label} title={t('nav.home')} subtitle={t('home.subtitle', { name: me.displayName })} actions={d && (editing ? <>
      {!d.isDefault && <Button variant="outline" onClick={restore}><RotateCcw className="size-4" />{t('home.restore')}</Button>}
      <Button onClick={() => setEditing(false)}>{t('home.done')}</Button>
    </> : <Button variant="outline" onClick={() => setEditing(true)}><Pencil className="size-4" />{t('home.edit')}</Button>)}>
      <WorkspaceTabs />
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      {!d ? !q.error && <Loading rows={8} /> : (
        <div className="space-y-6">
          {editing && <Notice icon={Pencil} title={t('home.edit')}>{t('home.editHint')}</Notice>}
          {shown.map((w) => {
            const i = layout.findIndex((x) => x.id === w.id)
            const name = t(`home.widget.${w.id}`)
            return (
              <div key={w.id} className={cn(editing && 'rounded-xl border border-dashed border-input p-3')}>
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
function ByProject({ d, scope }: { d: HomeData; scope: Scope }) {
  const [all, setAll] = useState(false)
  const rows = all ? d.tasksByProject : d.tasksByProject.slice(0, 8)
  const max = Math.max(1, ...d.tasksByProject.map((p) => p.total))
  return (
    <Section id="h-projects" accent="mint" title={t('home.byProject')} actions={note(t('home.currentShort'))}>
      {rows.length === 0 ? <Empty>{t('home.noTasks')}</Empty> : (
        <ul className="space-y-2 px-5 pt-5">
          {rows.map((p) => (
            <li key={p.projectId} className="grid grid-cols-[minmax(0,14rem)_1fr_3rem] items-center gap-3 text-sm">
              <span className="flex min-w-0 items-center gap-2" title={`${p.projectNumber} ${p.name}`}>
                <AccentDot id={p.projectId} /><span className="truncate"><Key>{p.projectNumber}</Key> {p.name}</span>
              </span>
              <span className="flex h-4 overflow-hidden rounded-full bg-secondary" style={{ width: `${(p.total / max) * 100}%` }}>
                {p.lanes.filter((l) => l.count > 0).map((l) => (
                  <Link key={l.lane} to={scoped(scope, '/tasks', { projectId: p.projectId, status: l.statuses })} className="block h-full hover:opacity-80"
                    style={{ width: `${(l.count / p.total) * 100}%`, background: LANE_TONE[l.lane] }}
                    aria-label={t('home.projectSegment', { project: p.projectNumber, lane: laneName(l.lane), n: l.count })} title={`${laneName(l.lane)}: ${l.count}`} />
                ))}
              </span>
              <Link to={scoped(scope, '/tasks', { projectId: p.projectId, status: p.lanes.map((l) => l.statuses).join(',') })} className="text-right font-medium tabular-nums hover:underline">{p.total}</Link>
            </li>
          ))}
        </ul>
      )}
      <div className="flex flex-wrap items-center gap-x-4 gap-y-2 px-5 py-4 text-xs/[18px] text-muted-foreground">
        {Object.entries(LANE_TONE).map(([lane, c]) => <span key={lane} className="flex items-center gap-1.5"><span className="inline-block size-2.5 rounded-full" style={{ background: c }} aria-hidden />{laneName(lane)}</span>)}
        {d.tasksByProject.length > 8 && <Button variant="link" size="sm" className="ml-auto" onClick={() => setAll(!all)}>{all ? t('home.fewer') : plural(d.tasksByProject.length, 'home.allOne', 'home.allMany', { n: d.tasksByProject.length })}</Button>}
      </div>
    </Section>
  )
}
