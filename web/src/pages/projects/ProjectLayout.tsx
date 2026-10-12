import { useQuery, useQueryClient } from '@tanstack/react-query'
import { CalendarClock, ChevronDown, ExternalLink, FolderOpen, MoreHorizontal, Star } from 'lucide-react'
import { createContext, useContext, useState } from 'react'
import { Link, NavLink, Outlet, useMatch, useParams } from 'react-router'
import { toast } from 'sonner'
import { ErrorBanner, InProject, Loading, Spinner } from '@/components/hub/common'
import { HealthPill, Key, StatusPill } from '@/components/hub/pills'
import { useProject, useProjectRefresh } from '@/hooks/data'
import { del, get, post } from '@/lib/api'
import { fmtDate, relative } from '@/lib/format'
import { plural, t } from '@/lib/i18n'
import type { ProjectDetail } from '@/lib/types'
import { accentOf, cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { useMe, type Me } from '@/lib/auth'
import { ProjectSlots } from './slots'

const Ctx = createContext<ProjectDetail | null>(null)
export const useCurrentProject = () => useContext(Ctx)!

type ProjectPage = { path: string | ((p: ProjectDetail) => string); label: string; show?: (p: ProjectDetail, me: Me) => boolean }
const page = (path: ProjectPage['path'], label: string, show?: ProjectPage['show']): ProjectPage => ({ path, label, show })

/** Project navigation (§9.4, §41 R3, decision E1): six groups plus More. Every page keeps its own route; the group that
 *  holds the current page is highlighted, and a group with several pages shows them in a second-level switch. A page
 *  appears only when its screen exists (§36.1). */
const PROJECT_GROUPS: { id: string; label: string; pages: ProjectPage[] }[] = [
  { id: 'overview', label: 'calm.group.overview', pages: [page('dashboard', 'ptab.dashboard')] },
  { id: 'work', label: 'calm.group.work', pages: [page('tasks', 'ptab.tasks'), page('board', 'ptab.board'), page('timeline', 'ptab.timeline'), page('calendar', 'ptab.calendar'), page('milestones', 'ptab.milestones')] },
  { id: 'deliverables', label: 'calm.group.deliverables', pages: [page('deliverables', 'ptab.deliverables'), page('submissions', 'ptab.submissions'), page('reviews', 'ptab.reviews'), page('readiness', 'ptab.readiness')] },
  { id: 'coordination', label: 'calm.group.coordination', pages: [page('coordination', 'ptab.coordination'), page('handoffs', 'ptab.handoffs'), page('changes', 'ptab.changes'), page('design-basis', 'ptab.designBasis'),
    page('decisions', 'ptab.decisions'), page('risks', 'ptab.risks'), page('issues', 'ptab.issues'), page('meetings', 'ptab.meetings')] },
  { id: 'people', label: 'calm.group.people', pages: [page('team', 'ptab.team'), page('allocations', 'ptab.allocations'), page((p) => `/workload?projectId=${p.id}`, 'ptab.workload', (_, me) => me.capabilities.workload)] },
  { id: 'files', label: 'calm.group.files', pages: [page('files', 'ptab.files')] },
]
/** Lower-frequency pages behind the More menu. */
const PROJECT_MORE: ProjectPage[] = [page('activity', 'ptab.activity'), page('settings', 'ptab.settings')]
/** Every project page, for breadcrumbs and route checks. */
export const PROJECT_TABS: ProjectPage[] = [...PROJECT_GROUPS.flatMap((g) => g.pages), ...PROJECT_MORE]

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
        <ProjectTabs p={p} me={me} />
        <StatusBanner p={p} />
        {p.status === 'Active' && (p.permissions.isPm || p.permissions.leadOf.length > 0) && <DateReviewBanner p={p} />}
        <div className="flex-1"><InProject.Provider value><Outlet /></InProject.Provider></div>
      </div>
    </Ctx.Provider>
  )
}

/** Group tabs, More for Activity and Settings, and the active group's pages as a small switch beside them (on a second
 *  line when they do not fit). All are real links, so a page opens in a new tab and history works as before. */
function ProjectTabs({ p, me }: { p: ProjectDetail; me: Me }) {
  const segment = useMatch('/projects/:number/*')?.params['*']?.split('/')[0]
  const visible = (pages: ProjectPage[]) => pages.filter((x) => !x.show || x.show(p, me))
  const to = (x: ProjectPage) => typeof x.path === 'string' ? x.path : x.path(p)
  const groups = PROJECT_GROUPS.map((g) => ({ ...g, pages: visible(g.pages) })).filter((g) => g.pages.length)
  const current = groups.find((g) => g.pages.some((x) => x.path === segment))
  const more = visible(PROJECT_MORE)
  const moreActive = more.find((x) => x.path === segment)
  const tab = (on: boolean) => cn('inline-flex min-h-11 items-center px-3 text-sm text-muted-foreground hover:bg-muted hover:text-foreground', on && 'font-semibold text-foreground shadow-[inset_0_-2px_0_var(--primary)]')
  return (
    <div className="no-print flex min-w-0 flex-wrap items-center gap-x-6 border-b bg-card px-4 md:sticky md:top-0 md:z-20 md:px-6 xl:px-8">
      <nav aria-label={t('ptab.label')} className="flex min-w-0 flex-wrap items-center gap-1">
        {groups.map((g) => <Link key={g.id} to={to(g.pages[0])} aria-current={g === current ? (g.pages[0].path === segment ? 'page' : 'true') : undefined} className={tab(g === current)}>{t(g.label)}</Link>)}
        {more.length > 0 && (
          <DropdownMenu>
            <DropdownMenuTrigger className={cn(tab(!!moreActive), 'gap-1')}>{moreActive ? t(moreActive.label) : t('calm.more')}<ChevronDown className="size-4" aria-hidden /></DropdownMenuTrigger>
            <DropdownMenuContent align="start">
              {more.map((x) => <DropdownMenuItem key={x.label} asChild><Link to={to(x)} aria-current={x === moreActive ? 'page' : undefined}>{t(x.label)}</Link></DropdownMenuItem>)}
            </DropdownMenuContent>
          </DropdownMenu>
        )}
      </nav>
      {current && current.pages.length > 1 && (
        <nav aria-label={t('calm.groupPages', { group: t(current.label) })} className="flex min-w-0 flex-wrap gap-0.5 py-1.5">
          {current.pages.map((x) => <NavLink key={x.label} to={to(x)} className={({ isActive }) => cn('inline-flex min-h-8 items-center rounded-md px-2.5 text-sm text-muted-foreground hover:bg-muted hover:text-foreground', isActive && 'bg-muted font-semibold text-foreground')}>{t(x.label)}</NavLink>)}
        </nav>
      )}
    </div>
  )
}

const linkCls = 'flex min-h-(--control-row-h) w-full items-center gap-2 rounded-sm px-3 py-1.5 text-left hover:bg-accent hover:text-accent-foreground focus-visible:bg-accent'

/** Shared project header (§12.1 UI behaviour, §13.0, §41 R6): one line with key, name, status and health with why. The
 *  client, PM, phase, next milestone and the project links sit in the details popover; Following keeps its own control. */
function ProjectHeader({ p }: { p: ProjectDetail }) {
  const refresh = useProjectRefresh()
  return (
    <header data-accent={accentOf(p.id)} className="flex min-w-0 items-center gap-3 border-b border-t-4 border-t-(color:--acc-stripe) bg-card px-4 py-2.5 md:px-6 xl:px-8">
      <button className="key shrink-0 rounded-md bg-(--acc-bg) px-2 py-0.5 text-(--acc-fg) hover:underline" title={t('common.copyLink')}
        onClick={() => { navigator.clipboard?.writeText(p.projectNumber); toast.success(t('common.copied')) }}>{p.projectNumber}</button>
      <h1 title={p.name} className="min-w-0 truncate text-lg/7 font-semibold tracking-[-0.2px]">{p.name}</h1>
      <button aria-label={p.starred ? t('projects.unstar') : t('projects.star')} aria-pressed={p.starred}
        onClick={async () => { await (p.starred ? del(`projects/${p.id}/star`) : post(`projects/${p.id}/star`)); refresh(p.id) }}
        className={cn('grid size-8 shrink-0 place-items-center rounded-md hover:bg-muted', p.starred ? 'text-warn' : 'text-muted-foreground')}><Star className={cn('size-4', p.starred && 'fill-current')} /></button>
      <div className="flex shrink-0 items-center gap-2 text-sm"><StatusPill status={p.status} /><ProjectSlots.Health p={p} /></div>
      <div className="ml-auto flex shrink-0 items-center gap-2">
        <ProjectSlots.Follow p={p} />
        {/* A labelled popover, not a menu: the details are text that keyboard and screen-reader users read in order. */}
        <Popover>
          <PopoverTrigger asChild><Button variant="ghost" size="icon" aria-label={t('calm.projectDetails')}><MoreHorizontal className="size-5" /></Button></PopoverTrigger>
          <PopoverContent align="end" aria-label={t('calm.projectDetails')} className="w-80 p-0 text-sm outline-none" tabIndex={-1}
            onOpenAutoFocus={(e) => { e.preventDefault(); (e.currentTarget as HTMLElement).focus() }}>
            <div className="space-y-1 px-4 py-3">
              <p className="font-semibold">{p.name}</p>
              <dl className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 text-muted-foreground">
                <dt>{t('projects.col.client')}</dt><dd className="text-foreground">{p.client}</dd>
                <dt>{t('projects.col.pm')}</dt><dd className="text-foreground">{p.pmName}</dd>
                {p.phase && <><dt>{t('projects.col.phase')}</dt><dd className="text-foreground">{p.phase}</dd></>}
                {p.nextMilestone && <><dt>{t('projects.nextMilestone')}</dt><dd className="text-foreground">◆ {p.nextMilestone.name} · {fmtDate(p.nextMilestone.date)} ({relative(p.nextMilestone.date)})</dd></>}
              </dl>
            </div>
            {p.links.length > 0 && (
              <ul className="border-t p-1">
                {p.links.slice(0, 4).map((l) => (
                  <li key={l.id}>
                    {l.linkType === 'Network Folder'
                      ? <button type="button" title={l.url} className={linkCls} onClick={() => { navigator.clipboard?.writeText(l.url); toast.success(t('links.pathCopied')) }}><FolderOpen className="size-4 text-muted-foreground" aria-hidden />{l.title}</button>
                      : <a href={l.url} target="_blank" rel="noreferrer noopener" className={linkCls}><ExternalLink className="size-4 text-muted-foreground" aria-hidden />{l.title}</a>}
                  </li>
                ))}
              </ul>
            )}
          </PopoverContent>
        </Popover>
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
    <div role="status" className="mx-4 mt-4 md:mx-6 xl:mx-8 flex flex-wrap items-center gap-x-3 gap-y-2 rounded-md border border-warn/30 bg-warn-bg px-3 py-2 text-sm">
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
      <div role="status" className="mx-4 mt-4 md:mx-6 xl:mx-8 rounded-md border border-warn/30 bg-warn-bg px-3 py-2 text-sm text-warn">
        <div className="font-medium">{t('projects.setupBanner')}</div>
        <ul className="mt-1 flex flex-wrap gap-x-5">{items.map(([k, ok]) => <li key={k}>{ok ? '✓' : '○'} {t(k)}</li>)}</ul>
      </div>
    )
  }
  if (p.status === 'On Hold') return <div role="status" className="mx-4 mt-4 md:mx-6 xl:mx-8 rounded-md border bg-idle-bg px-3 py-2 text-sm text-idle">{t('projects.onHoldBanner')}</div>
  if (p.status === 'Complete') return <div role="status" className="mx-4 mt-4 md:mx-6 xl:mx-8 rounded-md border border-done/30 bg-done-bg px-3 py-2 text-sm text-done">
    {p.suggestArchive ? t('projects.archiveSuggested') : t('projects.completeBanner', { n: p.editWindowDaysLeft ?? 0 })}</div>
  if (p.status === 'Archived' || p.status === 'Cancelled') return <div role="status" className="mx-4 mt-4 md:mx-6 xl:mx-8 rounded-md border bg-idle-bg px-3 py-2 text-sm text-idle">{t('projects.readOnlyBanner', { status: t(`value.${p.status}`) })}</div>
  return null
}

export { HealthPill, Key }
