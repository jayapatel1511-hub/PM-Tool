import { Bell, CheckSquare, ChevronDown, ChevronRight, ChevronsLeft, ChevronsRight, LogOut, Menu, MoreHorizontal, Search, User } from 'lucide-react'
import { useQuery } from '@tanstack/react-query'
import { Suspense, useEffect, useRef, useState } from 'react'
import { Link, NavLink, Outlet, useLocation, useMatch, useNavigate, useNavigation } from 'react-router'
import { useDensity, type Density } from '@/components/hub/common'
import { LeaveHost } from '@/components/hub/drafts'
import { PanelHost, useItemPanel } from '@/components/hub/panel-host'
import { Loading } from '@/components/hub/common'
import { Avatar } from '@/components/hub/people'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel, DropdownMenuRadioGroup, DropdownMenuRadioItem, DropdownMenuSeparator, DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { Sheet, SheetContent, SheetDescription, SheetTitle } from '@/components/ui/sheet'
import { useProject } from '@/hooks/data'
import { useLiveEvents } from '@/hooks/live'
import { useAuth, useFlag } from '@/lib/auth-context'
import { GUARD_TOASTS, pruneDrafts } from '@/lib/drafts'
import { typedRequest } from '@/lib/api'
import { t , runtimeText } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { PROJECT_TABS, PROJECT_VISUAL_PEOPLE_ALIASES } from '@/pages/projects/ProjectLayout'
import { BUILT, NAV, type NavItem } from './nav'
import { ShellSlots } from './slots'
import { useRouteCounter } from '@/lib/route-counter'
import { setVisualSystem } from '@/lib/visual-system'
import { captureRecordContextOrigin, findRecordContextSurface, recordContextDisclosures, recordContextReturn, type RecordContextSurfaceSlot } from '@/lib/record-context-navigation'

/** The application frame (§13.0, FR-022–FR-025, design §5): a 232 px sidebar at ≥ 1280 px (collapsible to the rail), a
 *  76 px icon rail with labelled tooltips from 768 px, a phone bottom bar below that, and a 64 px top bar. */
export function Shell() {
  const { me, signOut } = useAuth()
  useLiveEvents()
  const drafts = useFlag('data_entry_safety')
  const visualSystem = useFlag('visual_system')
  const peopleScreens = useFlag('people_screens')
  const openPanel = useItemPanel()
  const accountTrigger = useRef<HTMLButtonElement>(null)
  const keepDays = me.settings.draftKeepDays ?? 7
  useEffect(() => { if (drafts) pruneDrafts(keepDays) }, [drafts, keepDays]) // FR-SAV-03: a draft is dropped after draft_keep_days
  useEffect(() => { document.documentElement.toggleAttribute(GUARD_TOASTS, drafts) }, [drafts])
  useEffect(() => {
    setVisualSystem(visualSystem)
    return () => setVisualSystem(false)
  }, [visualSystem])
  const [collapsed, setCollapsed] = useState(() => { try { return localStorage.getItem('hub.rail') === 'collapsed' } catch { return false } })
  const [density, setDensity] = useDensity()
  const [phone, setPhone] = useState(false)
  const [menuOpen, setMenuOpen] = useState(false)
  const [searchOpen, setSearchOpen] = useState(false)
  const previousSearchRoute = useRef<{ url: string; personOrigin: string | null } | null>(null)
  const search = useRef<HTMLInputElement>(null)
  const phoneSearch = useRef<HTMLInputElement>(null)
  const navigate = useNavigate()
  const navigation = useNavigation()
  const location = useLocation()
  const { pathname } = location
  const restoreKey = useRef<string | null>(null)
  useRouteCounter(pathname)
  useEffect(() => {
    const restore = (location.state as { recordContextRestore?: unknown } | null)?.recordContextRestore
    if (!restore || typeof restore !== 'object' || !('scrollTop' in restore)) return
    const r = restore as { scrollTop?: unknown; scrollLeft?: unknown; focusId?: unknown; disclosures?: unknown; surface?: { slot?: unknown; scrollTop?: unknown; scrollLeft?: unknown } }
    const disclosures = recordContextDisclosures(r.disclosures)
    const focusId = typeof r.focusId === 'string' && r.focusId.length > 0 && r.focusId.length <= 200 && !/[\u0000-\u001f\u007f]/.test(r.focusId) ? r.focusId : null
    const top = typeof r.scrollTop === 'number' && Number.isFinite(r.scrollTop) ? Math.max(0, r.scrollTop) : 0
    const left = typeof r.scrollLeft === 'number' && Number.isFinite(r.scrollLeft) ? Math.max(0, r.scrollLeft) : 0
    const surface = r.surface && (r.surface.slot === 'dialog-body' || r.surface.slot === 'dialog-content' || r.surface.slot === 'sheet-content') ? {
      slot: r.surface.slot as RecordContextSurfaceSlot,
      top: typeof r.surface.scrollTop === 'number' && Number.isFinite(r.surface.scrollTop) ? Math.max(0, r.surface.scrollTop) : 0,
      left: typeof r.surface.scrollLeft === 'number' && Number.isFinite(r.surface.scrollLeft) ? Math.max(0, r.surface.scrollLeft) : 0,
    } : null
    const key = `${location.key}:${top}:${left}:${surface?.slot ?? ''}:${surface?.top ?? ''}:${surface?.left ?? ''}:${focusId ?? ''}:${JSON.stringify(disclosures)}`
    if (restoreKey.current === key) return
    restoreKey.current = key
    const content = document.getElementById('content')
    if (!content) return
    let frame = 0
    let stopped = false
    let observer: ResizeObserver | undefined
    let mutation: MutationObserver | undefined
    const observed = new WeakSet<Element>()
    const watch = (element: Element | null) => { if (element && !observed.has(element)) { observed.add(element); observer?.observe(element) } }
    const finish = () => { stopped = true; cancelAnimationFrame(frame); observer?.disconnect(); mutation?.disconnect(); window.removeEventListener('wheel', stop, true); window.removeEventListener('touchstart', stop, true); window.removeEventListener('keydown', stop, true) }
    const stop = () => { if (!stopped) finish() }
    const apply = () => {
      if (stopped) return
      watch(content.firstElementChild)
      const portal = surface ? findRecordContextSurface(surface.slot) : null
      const disclosureScope = surface ? portal : content
      if (disclosureScope) for (const disclosure of disclosures) {
        const element = document.getElementById(disclosure.id)
        if (element instanceof HTMLDetailsElement && disclosureScope.contains(element) && element.hasAttribute('data-person-source-disclosure') && element.open !== disclosure.open)
          element.open = disclosure.open
      }
      content.scrollTo({ top, left, behavior: 'auto' })
      if (portal && surface) { watch(portal); for (const child of portal.children) watch(child); portal.scrollTo({ top: surface.top, left: surface.left, behavior: 'auto' }) }
      const backgroundReached = Math.abs(content.scrollTop - top) <= 1 && Math.abs(content.scrollLeft - left) <= 1
      const portalReached = !surface || !!portal && Math.abs(portal.scrollTop - surface.top) <= 1 && Math.abs(portal.scrollLeft - surface.left) <= 1
      if (backgroundReached && portalReached) {
        if (focusId) {
          // Wait until portal teardown/autofocus finishes, then return to the real opener.
          cancelAnimationFrame(frame)
          frame = requestAnimationFrame(() => {
            const target = document.getElementById(focusId)
            // Closed trigger controls remain visible; only a closed content surface
            // excludes its descendants (for example a departing Sheet or popover).
            const shown = target instanceof HTMLElement && target.getClientRects().length > 0 && !target.closest('[hidden], [aria-hidden="true"], [data-state="closed"][data-slot$="-content"]')
            ;(shown ? target : content).focus({ preventScroll: true })
            finish()
          })
        } else finish()
      }
    }
    observer = new ResizeObserver(apply)
    watch(content)
    mutation = new MutationObserver(apply)
    mutation.observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['data-state', 'aria-hidden', 'hidden', 'open'] })
    window.addEventListener('wheel', stop, true); window.addEventListener('touchstart', stop, true); window.addEventListener('keydown', stop, true)
    apply()
    if (!stopped && !frame) frame = requestAnimationFrame(apply)
    return finish
  }, [location.key, location.state])
  const legacyItems = NAV.filter((n) => BUILT.has(n.id) && n.sidebar !== false && n.show(me))
  // Packet 052 is deliberately a presentation-only branch. Its one-list rail is selected
  // from the same permission-filtered NAV, so dark rollout never strands an existing route.
  const visualItems = NAV
    .filter((n) => BUILT.has(n.id) && n.sidebar !== false && n.visualGroup && n.show(me))
    .map((n) => ({ ...n, ...(n.visualPath ? { path: n.visualPath(me) } : {}), ...(n.visualAlso ? { also: [...(n.also ?? []), ...(typeof n.visualAlso === 'function' ? n.visualAlso(me, pathname) : n.visualAlso)] } : {}) }))
  const visualMainOrder = ['mywork', 'home', 'projects', 'tasks', 'planner', 'time', 'reports', 'team']
  const visualFootOrder = ['files', 'staff', 'templates', 'admin']
  const primary = visualSystem
    ? visualMainOrder.map((id) => visualItems.find((n) => n.id === id)).filter((n): n is NavItem => !!n)
    : legacyItems.filter((n) => n.primary)
  const foot = visualSystem
    ? visualFootOrder.map((id) => visualItems.find((n) => n.id === id)).filter((n): n is NavItem => !!n)
    : []
  const more = visualSystem ? [] : legacyItems.filter((n) => !n.primary)
  const label = collapsed ? 'sr-only' : 'sr-only xl:not-sr-only'
  // More stays collapsed (§41 R4) unless the current page lives only there; the viewer's toggle then wins for this visit.
  const [moreOpen, setMoreOpen] = useState<boolean | null>(null)
  const openMore = moreOpen ?? (more.some((n) => isActive(pathname, n)) && !primary.some((n) => isActive(pathname, n)))

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const el = e.target as HTMLElement
      if (e.key === '/' && !['INPUT', 'TEXTAREA', 'SELECT'].includes(el.tagName) && !el.isContentEditable) {
        if (visualSystem && el.closest('[role="dialog"], [role="alertdialog"]')) return
        e.preventDefault()
        if (visualSystem && phone) setSearchOpen(true)
        else search.current?.focus()
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [visualSystem, phone])
  useEffect(() => {
    const media = window.matchMedia('(max-width: 767.98px)')
    const update = () => {
      setPhone(media.matches)
      if (!media.matches || !visualSystem) { setMenuOpen(false); setSearchOpen(false) }
    }
    update(); media.addEventListener('change', update)
    return () => media.removeEventListener('change', update)
  }, [visualSystem])
  useEffect(() => {
    const url = `${location.pathname}${location.search}${location.hash}`
    const previous = previousSearchRoute.current
    const person = peopleScreens && new URLSearchParams(location.search).get('panel')?.startsWith('Person:')
    const origin = person ? recordContextReturn(location.state)?.url : null
    const inspectingPerson = !!origin && !!previous && (origin === previous.url || origin === previous.personOrigin)
    const returnedToSearch = !person && previous?.personOrigin === url
    // The phone Search sheet remains mounted underneath a Person overlay. Its term,
    // selected result and exact opener belong to that source until normal navigation.
    if (visualSystem) {
      setMenuOpen(false)
      if (!inspectingPerson && !returnedToSearch) setSearchOpen(false)
    }
    previousSearchRoute.current = { url, personOrigin: inspectingPerson ? previous?.personOrigin ?? origin ?? null : null }
  }, [location, visualSystem, peopleScreens])

  const toggle = () => {
    const c = !collapsed
    setCollapsed(c)
    try { localStorage.setItem('hub.rail', c ? 'collapsed' : 'open') } catch { /* per-viewer convenience only */ }
  }

  return (
    // The frame scrolls inside one viewport on screen; in print it flows so the whole page paginates.
    <div className="flex h-dvh overflow-hidden print:block print:h-auto print:overflow-visible">
      <a href="#content" className="sr-only focus:not-sr-only focus:absolute focus:left-2 focus:top-2 focus:z-50 focus:rounded-md focus:bg-card focus:px-3 focus:py-2 focus:shadow-popover">{t('app.skip')}</a>
      <nav aria-label={t('nav.main')} data-rail={collapsed ? 'collapsed' : 'open'} className={cn('visual-sidebar no-print hidden shrink-0 flex-col border-r bg-sidebar md:flex', collapsed ? 'w-[76px]' : 'w-[76px] xl:w-[232px]')}>
        <Link to="/" className={cn('flex h-16 shrink-0 items-center gap-2.5 px-5', collapsed ? 'justify-center' : 'justify-center xl:justify-start')}>
          <span aria-hidden data-visual-decoration="brand" data-accent="mint" className="grid size-8 shrink-0 place-items-center rounded-[10px] bg-(--acc-bg) text-lg font-semibold text-(--acc-fg)">t</span>
          <span className={cn('text-xl font-semibold tracking-[-0.4px]', label)}>{t('app.name')}</span>
        </Link>
        <div className="flex-1 overflow-y-auto px-3 pb-3">
          <ul className="space-y-1">
            {primary.map((n) => <li key={n.id}><RailLink item={n} collapsed={collapsed} /></li>)}
          </ul>
          {visualSystem && foot.length > 0 && <ul className="mt-4 space-y-1" aria-label={t('nav.more')}>
            {foot.map((n) => <li key={n.id}><RailLink item={n} collapsed={collapsed} /></li>)}
          </ul>}
          {!visualSystem && more.length > 0 && (
            <>
              <Tooltip>
                <TooltipTrigger asChild>
                  <button type="button" aria-expanded={openMore} aria-controls="nav-more" onClick={() => setMoreOpen(!openMore)}
                    className={cn('mt-4 flex min-h-11 w-full items-center gap-3 rounded-md text-sm text-muted-foreground hover:bg-sidebar-hover hover:text-foreground', collapsed ? 'justify-center' : 'justify-center xl:justify-start xl:px-3')}>
                    <MoreHorizontal className="size-5 shrink-0" aria-hidden />
                    <span className={cn('min-w-0 flex-1 truncate text-left', label)}>{t('nav.more')}</span>
                    <ChevronDown className={cn('size-4 shrink-0', openMore && 'rotate-180', collapsed ? 'hidden' : 'hidden xl:block')} aria-hidden />
                  </button>
                </TooltipTrigger>
                <TooltipContent side="right" className={collapsed ? '' : 'xl:hidden'}>{t('nav.more')}</TooltipContent>
              </Tooltip>
              {openMore && <ul id="nav-more" className="mt-1 space-y-1" aria-label={t('nav.more')}>
                {more.map((n) => <li key={n.id}><RailLink item={n} collapsed={collapsed} /></li>)}
              </ul>}
            </>
          )}
        </div>
        <button type="button" onClick={toggle} aria-label={collapsed ? t('nav.expand') : t('nav.collapse')}
          className="m-3 hidden min-h-10 items-center justify-center rounded-md text-muted-foreground hover:bg-sidebar-hover hover:text-foreground xl:flex">
          {collapsed ? <ChevronsRight className="size-5" /> : <ChevronsLeft className="size-5" />}
        </button>
      </nav>

      <div className="flex min-w-0 flex-1 flex-col pb-[calc(3.5rem+env(safe-area-inset-bottom))] md:pb-0 print:block print:pb-0">
        <header className="visual-topbar no-print flex h-16 shrink-0 items-center gap-2 border-b bg-card px-4 md:gap-3 md:px-6 xl:px-8">
          {visualSystem && phone && <button type="button" aria-label={t('nav.menu')} onClick={() => setMenuOpen(true)} className="grid size-9 shrink-0 place-items-center rounded-md text-muted-foreground hover:bg-muted hover:text-foreground md:hidden"><Menu className="size-5" aria-hidden /></button>}
          {visualSystem && phone && <Button variant="ghost" size="icon" aria-label={t('common.search')} onClick={() => setSearchOpen(true)} className="md:hidden"><Search className="size-5" aria-hidden /></Button>}
          <Breadcrumbs />
          <Suspense fallback={null}><div className={cn('min-w-0 flex-1 justify-end lg:flex-none', visualSystem ? 'hidden md:flex' : 'flex')}><ShellSlots.Search inputRef={search} /></div>
          <ShellSlots.Workspace />
          <ShellSlots.QuickCreate />
          <ShellSlots.Bell /></Suspense>
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button ref={accountTrigger} id={peopleScreens ? 'account-menu-trigger' : undefined} variant="ghost" className="gap-2 px-1.5 lg:px-2" aria-label={t('top.account')}>
                <Avatar id={me.id} name={me.displayName} className="size-8 text-xs" />
                <span className="hidden max-w-40 truncate 2xl:inline">{me.displayName}</span>
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-72">
              {peopleScreens && <DropdownMenuItem onSelect={() => openPanel('Person', me.id, captureRecordContextOrigin(location, accountTrigger.current))}>
                <User className="size-4" aria-hidden />{me.displayName}
              </DropdownMenuItem>}
              <DropdownMenuLabel>
                {!peopleScreens && <div className="font-semibold">{me.displayName}</div>}
                <div className="text-xs font-normal text-muted-foreground">{me.email}</div>
                <div className="mt-1 text-xs font-normal text-muted-foreground">
                  {t('top.roles')}: {[t('role.StandardUser'), ...me.systemRoles.map((r) => runtimeText(`role.${r}`))].join(', ')}
                </div>
              </DropdownMenuLabel>
              <DropdownMenuSeparator />
              <DropdownMenuLabel className="text-xs font-medium text-muted-foreground">{t('density.label')}</DropdownMenuLabel>
              <DropdownMenuRadioGroup value={density} onValueChange={(v) => setDensity(v as Density)}>
                <DropdownMenuRadioItem value="compact">{t('density.compact')}</DropdownMenuRadioItem>
                <DropdownMenuRadioItem value="comfortable">{t('density.comfortable')}</DropdownMenuRadioItem>
              </DropdownMenuRadioGroup>
              <DropdownMenuSeparator />
              <DropdownMenuItem onSelect={() => navigate('/preferences')}><User className="size-4" />{t('top.preferences')}</DropdownMenuItem>
              <DropdownMenuItem onSelect={signOut}><LogOut className="size-4" />{t('auth.signOut')}</DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </header>
        <main id="content" tabIndex={-1} className="relative min-h-0 flex-1 overflow-auto outline-none print:overflow-visible print:pb-0">
          <Suspense fallback={<Loading rows={6} />}><div aria-busy={navigation.state !== 'idle' || undefined}><Outlet /></div></Suspense>
        </main>
        <PanelHost />
        <LeaveHost />
      </div>

      <nav aria-label={t('nav.main')} className="no-print fixed inset-x-0 bottom-0 z-40 grid grid-cols-3 border-t bg-card pb-[env(safe-area-inset-bottom)] md:hidden">
        <BottomLink to="/my-work" icon={CheckSquare} label={t('nav.myWork')} />
        <button type="button" className="flex min-h-14 flex-col items-center justify-center gap-0.5 text-xs text-muted-foreground" onClick={() => visualSystem && phone ? setSearchOpen(true) : search.current?.focus()}>
          <Search className="size-5" aria-hidden />{t('common.search')}
        </button>
        <BottomLink to="/notifications" icon={Bell} label={t('nav.notifications')} />
      </nav>
      {visualSystem && phone && <>
        <Sheet open={menuOpen} onOpenChange={setMenuOpen}>
          <SheetContent side="left" className="w-[min(84vw,320px)] gap-0 p-0">
            <SheetTitle className="shrink-0 border-b px-5 py-4 text-base">{t('nav.main')}</SheetTitle>
            <SheetDescription className="sr-only">{t('nav.main')}</SheetDescription>
            <div className="min-h-0 flex-1 overflow-y-auto px-3 py-3">
              <ul className="space-y-1">{primary.map((n) => <li key={n.id}><RailLink item={n} collapsed={false} drawer onNavigate={() => setMenuOpen(false)} /></li>)}</ul>
              {foot.length > 0 && <ul className="mt-4 space-y-1 border-t pt-4">{foot.map((n) => <li key={n.id}><RailLink item={n} collapsed={false} drawer onNavigate={() => setMenuOpen(false)} /></li>)}</ul>}
            </div>
          </SheetContent>
        </Sheet>
        <Sheet open={searchOpen} onOpenChange={setSearchOpen}>
          <SheetContent side="top" className="gap-3 px-4 py-5">
            <SheetTitle>{t('common.search')}</SheetTitle>
            <SheetDescription className="sr-only">{t('top.search')}</SheetDescription>
            <Suspense fallback={null}><ShellSlots.Search inputRef={phoneSearch} onNavigate={() => setSearchOpen(false)} /></Suspense>
          </SheetContent>
        </Sheet>
      </>}
    </div>
  )
}

/** A route or one of its views (`also`) is the current page. */
const isActive = (pathname: string, n: NavItem) => [n.path, ...(n.also ?? [])].some((p) => pathname === p || pathname.startsWith(`${p}/`))

function RailLink({ item, collapsed, drawer = false, onNavigate }: { item: NavItem; collapsed: boolean; drawer?: boolean; onNavigate?: () => void }) {
  // A plain Link: the item is current for its own route and for its views (`also`), which NavLink cannot express.
  const active = isActive(useLocation().pathname, item)
  const { icon: Icon } = item
  const label = runtimeText(item.label)
  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <Link to={item.path} onClick={onNavigate} aria-current={active ? 'page' : undefined}
          className={cn('relative flex min-h-11 items-center gap-3 rounded-md text-sm text-muted-foreground hover:bg-sidebar-hover hover:text-foreground',
            drawer ? 'justify-start px-3' : collapsed ? 'justify-center' : 'justify-center xl:justify-start xl:px-3',
            active && 'bg-nav-active font-semibold text-nav-active-foreground before:absolute before:inset-y-2.5 before:left-0 before:w-[3px] before:rounded-full before:bg-nav-active-foreground hover:bg-nav-active hover:text-nav-active-foreground')}>
          <Icon className="size-5 shrink-0" aria-hidden />
          <span className={cn('min-w-0 truncate', !drawer && (collapsed ? 'sr-only' : 'sr-only xl:not-sr-only'))}>{label}</span>
        </Link>
      </TooltipTrigger>
      <TooltipContent side="right" className={drawer ? 'hidden' : collapsed ? '' : 'xl:hidden'}>{label}</TooltipContent>
    </Tooltip>
  )
}

function BottomLink({ to, icon: Icon, label }: { to: string; icon: React.ComponentType<{ className?: string }>; label: string }) {
  return (
    <NavLink to={to} className={({ isActive }) => cn('relative flex min-h-14 flex-col items-center justify-center gap-0.5 text-xs text-muted-foreground',
      isActive && 'font-semibold text-nav-active-foreground before:absolute before:inset-x-6 before:top-0 before:h-[3px] before:rounded-b-full before:bg-nav-active-foreground')}>
      <Icon className="size-5" aria-hidden />{label}
    </NavLink>
  )
}

/** Pages the navigation lists under another entry, or not at all, still name themselves in the breadcrumb. */
const CRUMBS: Record<string, string> = { '/timeline': 'wsview.timeline', '/search': 'search.title', '/preferences': 'top.preferences', '/planner': 'planner.title', '/workload': 'nav.workload', '/boards': 'nav.boards', '/calendar': 'nav.calendar', '/admin/users': 'admin.users', '/admin/settings': 'admin.settings', '/admin/activity': 'admin.activity', '/admin/operations': 'ops.title', '/admin/holidays': 'holiday.title', '/admin/review': 'adminReview.title' }

/** Where you are: the app, the section from the navigation, and inside a project its number, name and tab. The project
 *  name comes from the same cached query as the project header, so this adds no request. */
function Breadcrumbs() {
  const { pathname } = useLocation()
  const { me } = useAuth()
  const visualSystem = useFlag('visual_system')
  const inProject = useMatch('/projects/:number/*')
  const number = inProject?.params.number
  const project = useProject(number)
  const taskMatch = useMatch('/tasks/:id')
  const taskId = taskMatch?.params.id
  const task = useQuery({ queryKey: ['task', taskId], enabled: !!taskId, queryFn: () => typedRequest('get', '/api/v1/tasks/{id}', { params: { id: taskId! } }) })
  const crumbs: { label: string; to?: string; long?: boolean }[] = []
  if (number) {
    const tabs = visualSystem && !me.capabilities.readOnly ? [...PROJECT_TABS, ...PROJECT_VISUAL_PEOPLE_ALIASES] : PROJECT_TABS
    const tab = tabs.find((tb) => typeof tb.path === 'string' && tb.path === inProject.params['*']?.split('/')[0])
    crumbs.push({ label: t('nav.projects'), to: '/projects' })
    crumbs.push({ label: project.data ? `${project.data.projectNumber} ${project.data.name}` : number, to: tab ? `/projects/${encodeURIComponent(number)}` : undefined, long: true })
    if (tab) crumbs.push({ label: runtimeText(tab.label) })
  } else if (/^\/people\/[^/]+\/reassign-work$/.test(pathname)) {
    crumbs.push({ label: t('nav.staff'), to: '/staff' }, { label: t('reassign.action') })
  } else if (taskId) {
    crumbs.push({ label: t('nav.tasks'), to: '/tasks' }, { label: task.data?.task.key ?? t('itemType.Task'), long: true })
  } else if (pathname.startsWith('/admin/reference/')) {
    crumbs.push({ label: t('nav.admin'), to: '/admin' }, { label: runtimeText(`admin.${pathname.split('/').pop()}`) })
  } else if (CRUMBS[pathname]) {
    if (pathname.startsWith('/admin/')) crumbs.push({ label: t('nav.admin'), to: '/admin' })
    if (pathname === '/planner' || pathname === '/workload') crumbs.push({ label: t('nav.resources') })
    crumbs.push({ label: runtimeText(CRUMBS[pathname]) })
  } else {
    const n = NAV.find((x) => pathname === x.path || pathname.startsWith(`${x.path}/`))
    if (n) crumbs.push({ label: runtimeText(n.label), to: pathname === n.path ? undefined : n.path })
  }
  return (
    <nav aria-label={t('nav.breadcrumb')} className="hidden min-w-0 flex-1 lg:block">
      <ol className="flex min-w-0 flex-nowrap items-center gap-x-1.5 overflow-hidden text-sm text-muted-foreground">
        <li className="shrink-0"><Link to="/" className="block rounded-sm leading-6 hover:text-foreground hover:underline">{t('app.name')}</Link></li>
        {crumbs.map((c, i) => (
          <li key={i} className={cn('inline-flex items-center gap-1.5', c.long ? 'min-w-16 flex-1 basis-16' : 'shrink-0', c.long === undefined && crumbs.length > 2 && i === 0 && 'hidden xl:inline-flex')} title={c.label}>
            <ChevronRight className="size-3.5 shrink-0" aria-hidden />
            {c.to ? <Link to={c.to} className="block min-w-0 truncate rounded-sm leading-6 hover:text-foreground hover:underline">{c.label}</Link>
              : <span aria-current="page" className="block min-w-0 truncate font-medium leading-6 text-foreground">{c.label}</span>}
          </li>
        ))}
      </ol>
    </nav>
  )
}
