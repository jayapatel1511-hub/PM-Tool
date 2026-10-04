import { Bell, CheckSquare, ChevronRight, ChevronsLeft, ChevronsRight, LogOut, Search, User } from 'lucide-react'
import { Fragment, useEffect, useRef, useState } from 'react'
import { Link, NavLink, Outlet, useLocation, useMatch, useNavigate } from 'react-router'
import { useDensity, type Density } from '@/components/hub/common'
import { PanelHost } from '@/components/hub/panel-host'
import { Avatar } from '@/components/hub/people'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel, DropdownMenuRadioGroup, DropdownMenuRadioItem, DropdownMenuSeparator, DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { useProject } from '@/hooks/data'
import { useAuth } from '@/lib/auth'
import { t } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { PROJECT_TABS } from '@/pages/projects/ProjectLayout'
import { BUILT, NAV } from './nav'
import { ShellSlots } from './slots'

/** The application frame (§13.0, FR-022–FR-025, design §5): a 232 px sidebar at ≥ 1280 px (collapsible to the rail), a
 *  76 px icon rail with labelled tooltips from 768 px, a phone bottom bar below that, and a 64 px top bar. */
export function Shell() {
  const { me, signOut } = useAuth()
  const [collapsed, setCollapsed] = useState(() => { try { return localStorage.getItem('hub.rail') === 'collapsed' } catch { return false } })
  const [density, setDensity] = useDensity()
  const search = useRef<HTMLInputElement>(null)
  const navigate = useNavigate()
  const items = NAV.filter((n) => BUILT.has(n.id) && n.show(me))
  const primary = items.filter((n) => n.primary)
  const more = items.filter((n) => !n.primary)
  const label = collapsed ? 'sr-only' : 'sr-only xl:not-sr-only'

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const el = e.target as HTMLElement
      if (e.key === '/' && !['INPUT', 'TEXTAREA', 'SELECT'].includes(el.tagName) && !el.isContentEditable) {
        e.preventDefault()
        search.current?.focus()
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])

  const toggle = () => {
    const c = !collapsed
    setCollapsed(c)
    try { localStorage.setItem('hub.rail', c ? 'collapsed' : 'open') } catch { /* per-viewer convenience only */ }
  }

  return (
    // The frame scrolls inside one viewport on screen; in print it flows so the whole page paginates.
    <div className="flex h-dvh overflow-hidden print:block print:h-auto print:overflow-visible">
      <a href="#content" className="sr-only focus:not-sr-only focus:absolute focus:left-2 focus:top-2 focus:z-50 focus:rounded-md focus:bg-card focus:px-3 focus:py-2 focus:shadow-popover">{t('app.skip')}</a>
      <nav aria-label={t('nav.main')} className={cn('no-print hidden shrink-0 flex-col border-r bg-sidebar md:flex', collapsed ? 'w-[76px]' : 'w-[76px] xl:w-[232px]')}>
        <Link to="/" className={cn('flex h-16 shrink-0 items-center gap-2.5 px-5', collapsed ? 'justify-center' : 'justify-center xl:justify-start')}>
          <span aria-hidden data-accent="mint" className="grid size-8 shrink-0 place-items-center rounded-[10px] bg-(--acc-bg) text-lg font-semibold text-(--acc-fg)">t</span>
          <span className={cn('text-xl font-semibold tracking-[-0.4px]', label)}>{t('app.name')}</span>
        </Link>
        <div className="flex-1 overflow-y-auto px-3 pb-3">
          <ul className="space-y-1">
            {primary.map((n) => <li key={n.id}><RailLink to={n.path} icon={n.icon} label={t(n.label)} collapsed={collapsed} /></li>)}
          </ul>
          {more.length > 0 && (
            <>
              <p id="nav-more" className={cn('px-3 pb-2 pt-6 text-xs/[18px] font-semibold uppercase tracking-[1px] text-muted-foreground', label)}>{t('nav.more')}</p>
              <div aria-hidden className={cn('mx-2 my-3 border-t', !collapsed && 'xl:hidden')} />
              <ul className="space-y-1" aria-labelledby="nav-more">
                {more.map((n) => <li key={n.id}><RailLink to={n.path} icon={n.icon} label={t(n.label)} collapsed={collapsed} /></li>)}
              </ul>
            </>
          )}
        </div>
        <button type="button" onClick={toggle} aria-label={collapsed ? t('nav.expand') : t('nav.collapse')}
          className="m-3 hidden min-h-10 items-center justify-center rounded-md text-muted-foreground hover:bg-sidebar-hover hover:text-foreground xl:flex">
          {collapsed ? <ChevronsRight className="size-5" /> : <ChevronsLeft className="size-5" />}
        </button>
      </nav>

      <div className="flex min-w-0 flex-1 flex-col print:block">
        <header className="no-print flex h-16 shrink-0 items-center gap-2 border-b bg-card px-4 md:gap-3 md:px-6 xl:px-8">
          <Breadcrumbs />
          <div className="flex min-w-0 flex-1 justify-end lg:flex-none"><ShellSlots.Search inputRef={search} /></div>
          <ShellSlots.Workspace />
          <ShellSlots.QuickCreate />
          <ShellSlots.Bell />
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" className="gap-2 px-1.5 lg:px-2" aria-label={t('top.account')}>
                <Avatar id={me.id} name={me.displayName} className="size-8 text-xs" />
                <span className="hidden max-w-40 truncate 2xl:inline">{me.displayName}</span>
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-72">
              <DropdownMenuLabel>
                <div className="font-semibold">{me.displayName}</div>
                <div className="text-xs font-normal text-muted-foreground">{me.email}</div>
                <div className="mt-1 text-xs font-normal text-muted-foreground">
                  {t('top.roles')}: {[t('role.StandardUser'), ...me.systemRoles.map((r) => t(`role.${r}`))].join(', ')}
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
        <main id="content" tabIndex={-1} className="relative min-h-0 flex-1 overflow-auto pb-20 outline-none md:pb-0 print:overflow-visible print:pb-0">
          <Outlet />
        </main>
        <PanelHost />
      </div>

      <nav aria-label={t('nav.main')} className="no-print fixed inset-x-0 bottom-0 z-40 grid grid-cols-3 border-t bg-card pb-[env(safe-area-inset-bottom)] md:hidden">
        <BottomLink to="/my-work" icon={CheckSquare} label={t('nav.myWork')} />
        <button type="button" className="flex min-h-14 flex-col items-center justify-center gap-0.5 text-xs text-muted-foreground" onClick={() => search.current?.focus()}>
          <Search className="size-5" aria-hidden />{t('common.search')}
        </button>
        <BottomLink to="/notifications" icon={Bell} label={t('nav.notifications')} />
      </nav>
    </div>
  )
}

function RailLink({ to, icon: Icon, label, collapsed }: { to: string; icon: React.ComponentType<{ className?: string }>; label: string; collapsed: boolean }) {
  // Class computed here (not NavLink's function form) so the tooltip trigger can merge props.
  const active = !!useMatch({ path: to, end: false })
  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <NavLink to={to} aria-current={active ? 'page' : undefined}
          className={cn('relative flex min-h-11 items-center gap-3 rounded-md text-sm text-muted-foreground hover:bg-sidebar-hover hover:text-foreground',
            collapsed ? 'justify-center' : 'justify-center xl:justify-start xl:px-3',
            active && 'bg-nav-active font-semibold text-nav-active-foreground before:absolute before:inset-y-2.5 before:left-0 before:w-[3px] before:rounded-full before:bg-nav-active-foreground hover:bg-nav-active hover:text-nav-active-foreground')}>
          <Icon className="size-5 shrink-0" aria-hidden />
          <span className={cn('min-w-0 truncate', collapsed ? 'sr-only' : 'sr-only xl:not-sr-only')}>{label}</span>
        </NavLink>
      </TooltipTrigger>
      <TooltipContent side="right" className={collapsed ? '' : 'xl:hidden'}>{label}</TooltipContent>
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

/** Where you are: the app, the section from the navigation, and inside a project its number, name and tab. The project
 *  name comes from the same cached query as the project header, so this adds no request. */
function Breadcrumbs() {
  const { pathname } = useLocation()
  const inProject = useMatch('/projects/:number/*')
  const number = inProject?.params.number
  const project = useProject(number)
  const crumbs: { label: string; to?: string; long?: boolean }[] = []
  if (number) {
    const tab = PROJECT_TABS.find((tb) => typeof tb.path === 'string' && tb.path === inProject.params['*']?.split('/')[0])
    crumbs.push({ label: t('nav.projects'), to: '/projects' })
    crumbs.push({ label: project.data ? `${project.data.projectNumber} ${project.data.name}` : number, to: tab ? `/projects/${encodeURIComponent(number)}` : undefined, long: true })
    if (tab) crumbs.push({ label: t(tab.label) })
  } else if (/^\/people\/[^/]+\/reassign-work$/.test(pathname)) {
    crumbs.push({ label: t('nav.staff'), to: '/staff' }, { label: t('reassign.action') })
  } else {
    const n = NAV.find((x) => pathname === x.path || pathname.startsWith(`${x.path}/`))
    if (n) crumbs.push({ label: t(n.label), to: pathname === n.path ? undefined : n.path })
  }
  return (
    <nav aria-label={t('nav.breadcrumb')} className="hidden min-w-0 flex-1 lg:block">
      <ol className="flex min-w-0 items-center gap-1.5 text-sm text-muted-foreground">
        <li className="shrink-0"><Link to="/" className="block rounded-sm leading-6 hover:text-foreground hover:underline">{t('app.name')}</Link></li>
        {crumbs.map((c, i) => (
          <Fragment key={i}>
            <li aria-hidden className="shrink-0"><ChevronRight className="size-3.5" /></li>
            <li className={c.long ? 'min-w-0' : 'shrink-0 whitespace-nowrap'} title={c.label}>
              {c.to ? <Link to={c.to} className="block truncate rounded-sm leading-6 hover:text-foreground hover:underline">{c.label}</Link>
                : <span aria-current="page" className="block truncate font-medium leading-6 text-foreground">{c.label}</span>}
            </li>
          </Fragment>
        ))}
      </ol>
    </nav>
  )
}
