import { useQuery, useQueryClient } from '@tanstack/react-query'
import { CalendarClock, ExternalLink, FolderOpen, Star } from 'lucide-react'
import { createContext, useContext, useState } from 'react'
import { NavLink, Outlet, useParams } from 'react-router'
import { toast } from 'sonner'
import { ErrorBanner, Loading, Spinner } from '@/components/hub/common'
import { HealthPill, Key, StatusPill } from '@/components/hub/pills'
import { useProject, useProjectRefresh } from '@/hooks/data'
import { del, get, post } from '@/lib/api'
import { fmtDate, relative } from '@/lib/format'
import { plural, t } from '@/lib/i18n'
import type { ProjectDetail } from '@/lib/types'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { useMe, type Me } from '@/lib/auth'
import { ProjectSlots } from './slots'

const Ctx = createContext<ProjectDetail | null>(null)
export const useCurrentProject = () => useContext(Ctx)!

/** Project tabs (§9.4). A tab appears only when its screen exists (§36.1). */
export const PROJECT_TABS: { path: string | ((p: ProjectDetail) => string); label: string; show?: (p: ProjectDetail, me: Me) => boolean }[] = [
  { path: 'dashboard', label: 'ptab.dashboard' },
  { path: 'coordination', label: 'ptab.coordination' },
  { path: 'tasks', label: 'ptab.tasks' },
  { path: 'board', label: 'ptab.board' },
  { path: 'deliverables', label: 'ptab.deliverables' },
  { path: 'milestones', label: 'ptab.milestones' },
  { path: 'timeline', label: 'ptab.timeline' },
  { path: 'decisions', label: 'ptab.decisions' },
  { path: 'risks', label: 'ptab.risks' },
  { path: 'issues', label: 'ptab.issues' },
  { path: 'meetings', label: 'ptab.meetings' },
  { path: 'files', label: 'ptab.files' },
  { path: 'calendar', label: 'ptab.calendar' },
  { path: (p) => `/workload?projectId=${p.id}`, label: 'ptab.workload', show: (_, me) => me.capabilities.workload },
  { path: 'team', label: 'ptab.team' },
  { path: 'activity', label: 'ptab.activity' },
  { path: 'settings', label: 'ptab.settings' },
]

export function ProjectLayout() {
  const { number } = useParams()
  const me = useMe()
  const q = useProject(number)
  if (q.isPending) return <Loading rows={6} />
  if (q.error) return <div className="p-6"><ErrorBanner error={q.error} retry={() => q.refetch()} /></div>
  const p = q.data
  return (
    <Ctx.Provider value={p}>
      <div className="flex min-h-full flex-col">
        <ProjectHeader p={p} />
        <nav aria-label={t('ptab.label')} className="no-print sticky top-0 z-20 flex gap-1 overflow-x-auto border-b bg-card px-4">
          {PROJECT_TABS.filter((tab) => !tab.show || tab.show(p, me)).map((tab) => (
            <NavLink key={tab.label} to={typeof tab.path === 'string' ? tab.path : tab.path(p)} className={({ isActive }) => cn('whitespace-nowrap border-b-2 border-transparent px-3 py-2 text-sm text-muted-foreground hover:text-foreground',
              isActive && 'border-primary font-medium text-foreground')}>{t(tab.label)}</NavLink>
          ))}
        </nav>
        <StatusBanner p={p} />
        {p.status === 'Active' && (p.permissions.isPm || p.permissions.leadOf.length > 0) && <DateReviewBanner p={p} />}
        <div className="flex-1"><Outlet /></div>
      </div>
    </Ctx.Provider>
  )
}

/** Shared project header (§12.1 UI behaviour, §13.0): key, name, client, PM, phase, status, health with why, next milestone, links, follow. */
function ProjectHeader({ p }: { p: ProjectDetail }) {
  const refresh = useProjectRefresh()
  return (
    <header className="flex flex-wrap items-start justify-between gap-3 border-b bg-card px-4 pb-3 pt-4">
      <div className="min-w-0 space-y-1">
        <div className="flex flex-wrap items-center gap-2">
          <button className="key rounded bg-muted px-1.5 py-0.5 text-xs hover:bg-accent" title={t('common.copyLink')}
            onClick={() => { navigator.clipboard?.writeText(p.projectNumber); toast.success(t('common.copied')) }}>{p.projectNumber}</button>
          <h1 className="truncate text-xl font-semibold tracking-tight">{p.name}</h1>
          <button aria-label={p.starred ? t('projects.unstar') : t('projects.star')} aria-pressed={p.starred}
            onClick={async () => { await (p.starred ? del(`projects/${p.id}/star`) : post(`projects/${p.id}/star`)); refresh(p.id) }}
            className={cn('rounded p-1 hover:bg-muted', p.starred ? 'text-warn' : 'text-muted-foreground/60')}><Star className={cn('size-4', p.starred && 'fill-current')} /></button>
        </div>
        <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-muted-foreground">
          <span>{p.client}</span>
          <span>{t('projects.col.pm')}: <span className="text-foreground">{p.pmName}</span></span>
          {p.phase && <span>{t('projects.col.phase')}: <span className="text-foreground">{p.phase}</span></span>}
          <StatusPill status={p.status} />
          <ProjectSlots.Health p={p} />
          {p.nextMilestone && (
            <span title={t('projects.nextMilestone')}>
              ◆ {p.nextMilestone.name} · {fmtDate(p.nextMilestone.date)} <span className="text-foreground">({relative(p.nextMilestone.date)})</span>
            </span>
          )}
        </div>
      </div>
      <div className="flex flex-wrap items-center gap-2">
        {p.links.slice(0, 4).map((l) => (
          l.linkType === 'Network Folder'
            ? <button key={l.id} className="inline-flex items-center gap-1 rounded-md border px-2 py-1 text-xs hover:bg-muted" title={l.url}
                onClick={() => { navigator.clipboard?.writeText(l.url); toast.success(t('links.pathCopied')) }}><FolderOpen className="size-3.5" />{l.title}</button>
            : <a key={l.id} href={l.url} target="_blank" rel="noreferrer noopener" className="inline-flex items-center gap-1 rounded-md border px-2 py-1 text-xs hover:bg-muted">
                <ExternalLink className="size-3.5" />{l.title}</a>
        ))}
        <ProjectSlots.Follow p={p} />
      </div>
    </header>
  )
}

/** Banners explaining why evaluation is paused, the Setup checklist, and the archive suggestion (§13.1, P-05, P-09, G-05). */
interface DateReview { window: { heldFrom: string; resumedOn: string; days: number } | null; tasks: { id: string }[]; deliverables: { id: string }[] }

/** E-05 (Rec): back from On Hold, the work whose dates fell during the hold, with one action to shift it all. */
function DateReviewBanner({ p }: { p: ProjectDetail }) {
  const qc = useQueryClient()
  const refresh = useProjectRefresh()
  const q = useQuery({ queryKey: ['p', p.id, 'date-review'], queryFn: () => get<DateReview>(`projects/${p.id}/date-review`) })
  const key = `hub.dateReview.${p.id}.${q.data?.window?.resumedOn}`
  const [, rerender] = useState(0)
  const hidden = (() => { try { return localStorage.getItem(key) === '1' } catch { return false } })()
  const [days, setDays] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const r = q.data
  if (!r?.window || hidden || r.tasks.length + r.deliverables.length === 0) return null
  const n = Number(days ?? r.window.days) || 0
  const shift = async () => {
    setBusy(true)
    try {
      const reason = t('dateReview.reason', { from: fmtDate(r.window!.heldFrom), to: fmtDate(r.window!.resumedOn) })
      if (r.tasks.length) await post(`projects/${p.id}/tasks/bulk`, { taskIds: r.tasks.map((x) => x.id), operation: 'shiftDueDates', params: { days: n }, reason })
      if (r.deliverables.length) await post(`projects/${p.id}/deliverables/bulk`, { ids: r.deliverables.map((x) => x.id), operation: 'shiftDueDates', params: { days: n }, reason })
      toast.success(t('dateReview.shifted', { n })); qc.invalidateQueries({ queryKey: ['p', p.id, 'date-review'] }); refresh(p.id)
    } catch (e) { toast.error((e as Error).message) } finally { setBusy(false) }
  }
  const dismiss = () => { try { localStorage.setItem(key, '1') } catch { /* per-viewer convenience only */ } rerender((x) => x + 1) }
  return (
    <div role="status" className="mx-4 mt-3 flex flex-wrap items-center gap-x-3 gap-y-2 rounded-md border border-warn/30 bg-warn-bg px-3 py-2 text-sm">
      <CalendarClock className="size-4 shrink-0 text-warn" aria-hidden />
      <span className="min-w-0 flex-1">{t('dateReview.text', { what: [r.tasks.length && plural(r.tasks.length, 'dateReview.task1', 'dateReview.tasksN'),
        r.deliverables.length && plural(r.deliverables.length, 'dateReview.deliverable1', 'dateReview.deliverablesN')].filter(Boolean).join(t('dateReview.and')),
        were: t(r.tasks.length + r.deliverables.length === 1 ? 'dateReview.was' : 'dateReview.were'), from: fmtDate(r.window.heldFrom), to: fmtDate(r.window.resumedOn) })}</span>
      <label className="flex items-center gap-1.5">{t('dateReview.shiftBy')}
        <Input type="number" min={1} max={365} className="h-8 w-20" value={days ?? String(r.window.days)} onChange={(e) => setDays(e.target.value)} />{t('dateReview.days')}</label>
      <Button size="sm" disabled={busy || n <= 0} onClick={shift}>{busy && <Spinner />}{t('dateReview.shift')}</Button>
      <Button size="sm" variant="ghost" onClick={dismiss}>{t('dateReview.dismiss')}</Button>
    </div>
  )
}

function StatusBanner({ p }: { p: ProjectDetail }) {
  const c = p.setupChecklist
  if (p.status === 'Setup' && c) {
    const items = [['projects.check.milestone', c.hasMilestone], ['projects.check.leads', c.everyDisciplineHasLead], ['projects.check.submissions', c.everySubmissionHasDeliverable]] as const
    return (
      <div role="status" className="mx-4 mt-3 rounded-md border border-warn/30 bg-warn-bg px-3 py-2 text-sm text-warn">
        <div className="font-medium">{t('projects.setupBanner')}</div>
        <ul className="mt-1 flex flex-wrap gap-x-5">{items.map(([k, ok]) => <li key={k}>{ok ? '✓' : '○'} {t(k)}</li>)}</ul>
      </div>
    )
  }
  if (p.status === 'On Hold') return <div role="status" className="mx-4 mt-3 rounded-md border bg-idle-bg px-3 py-2 text-sm text-idle">{t('projects.onHoldBanner')}</div>
  if (p.status === 'Complete') return <div role="status" className="mx-4 mt-3 rounded-md border border-done/30 bg-done-bg px-3 py-2 text-sm text-done">
    {p.suggestArchive ? t('projects.archiveSuggested') : t('projects.completeBanner', { n: p.editWindowDaysLeft ?? 0 })}</div>
  if (p.status === 'Archived' || p.status === 'Cancelled') return <div role="status" className="mx-4 mt-3 rounded-md border bg-idle-bg px-3 py-2 text-sm text-idle">{t('projects.readOnlyBanner', { status: t(`value.${p.status}`) })}</div>
  return null
}

export { HealthPill, Key }
