import { ChevronsLeft, ChevronsRight, LogOut, Search, User } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { NavLink, Outlet, useMatch, useNavigate } from 'react-router'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel, DropdownMenuSeparator, DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { useAuth } from '@/lib/auth'
import { t } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { BUILT, NAV } from './nav'
import { ShellSlots } from './slots'
import { PanelHost } from '@/components/hub/panel-host'

/** The application frame (§13.0, FR-022–FR-025): role-filtered rail, top bar, and a phone bottom bar. */
export function Shell() {
  const { me, signOut } = useAuth()
  const [collapsed, setCollapsed] = useState(() => localStorage.getItem('hub.rail') === 'collapsed')
  const search = useRef<HTMLInputElement>(null)
  const navigate = useNavigate()
  const items = NAV.filter((n) => BUILT.has(n.id) && n.show(me))
  const primary = items.filter((n) => n.primary)
  const more = items.filter((n) => !n.primary)

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

  const toggle = () => { const c = !collapsed; setCollapsed(c); localStorage.setItem('hub.rail', c ? 'collapsed' : 'open') }

  return (
    <div className="flex h-dvh overflow-hidden">
      <a href="#content" className="sr-only focus:not-sr-only focus:absolute focus:z-50 focus:bg-card focus:p-2">{t('app.skip')}</a>
      <nav aria-label={t('nav.main')} className={cn('hidden shrink-0 flex-col bg-frame text-frame-foreground md:flex', collapsed ? 'w-14' : 'w-14 xl:w-52')}>
        <div className="flex h-12 items-center gap-2 px-3 font-semibold">
          <span aria-hidden className="grid size-7 place-items-center rounded-md bg-frame-active text-xs text-white">T</span>
          <span className={cn('truncate', collapsed ? 'hidden' : 'hidden xl:inline')}>{t('app.name')}</span>
        </div>
        <ul className="flex-1 space-y-0.5 overflow-y-auto px-2 py-2">
          {[...primary, ...more].map((n, i) => (
            <li key={n.id} className={i === primary.length && more.length ? 'mt-3 border-t border-white/10 pt-3' : undefined}>
              <RailLink to={n.path} icon={n.icon} label={t(n.label)} collapsed={collapsed} />
            </li>
          ))}
        </ul>
        <button onClick={toggle} className="m-2 flex items-center justify-center gap-2 rounded-md p-2 text-frame-muted hover:bg-white/10 hover:text-white"
          aria-label={collapsed ? t('nav.expand') : t('nav.collapse')}>
          {collapsed ? <ChevronsRight className="size-4" /> : <ChevronsLeft className="size-4" />}
        </button>
      </nav>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="no-print flex h-12 shrink-0 items-center gap-2 border-b bg-card px-3">
          <ShellSlots.Search inputRef={search} />
          <div className="flex-1" />
          <ShellSlots.Workspace />
          <ShellSlots.QuickCreate />
          <ShellSlots.Bell />
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" size="sm" className="gap-2" aria-label={t('top.account')}>
                <span aria-hidden className="grid size-7 place-items-center rounded-full bg-primary text-xs font-semibold text-primary-foreground">
                  {me.displayName.split(' ').map((p) => p[0]).slice(0, 2).join('')}
                </span>
                <span className="hidden lg:inline">{me.displayName}</span>
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-64">
              <DropdownMenuLabel>
                <div className="font-medium">{me.displayName}</div>
                <div className="text-xs font-normal text-muted-foreground">{me.email}</div>
                <div className="mt-1 text-xs font-normal text-muted-foreground">
                  {t('top.roles')}: {[t('role.StandardUser'), ...me.systemRoles.map((r) => t(`role.${r}`))].join(', ')}
                </div>
              </DropdownMenuLabel>
              <DropdownMenuSeparator />
              <DropdownMenuItem onSelect={() => navigate('/preferences')}><User className="size-4" />{t('top.preferences')}</DropdownMenuItem>
              <DropdownMenuItem onSelect={signOut}><LogOut className="size-4" />{t('auth.signOut')}</DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </header>
        <main id="content" tabIndex={-1} className="min-h-0 flex-1 overflow-auto pb-16 outline-none md:pb-0">
          <Outlet />
        </main>
        <PanelHost />
      </div>

      <nav aria-label={t('nav.main')} className="no-print fixed inset-x-0 bottom-0 z-40 grid h-14 grid-cols-3 border-t bg-card md:hidden">
        <NavLink to="/my-work" className={({ isActive }) => cn('grid place-items-center text-xs', isActive && 'text-primary')}>{t('nav.myWork')}</NavLink>
        <button className="grid place-items-center text-xs" onClick={() => search.current?.focus()}><Search className="size-4" />{t('common.search')}</button>
        <NavLink to="/notifications" className={({ isActive }) => cn('grid place-items-center text-xs', isActive && 'text-primary')}>{t('nav.notifications')}</NavLink>
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
          className={cn('flex items-center gap-3 rounded-md px-2.5 py-2 text-sm text-frame-muted hover:bg-white/10 hover:text-white', active && 'bg-frame-active text-white hover:bg-frame-active')}>
          <Icon className="size-4 shrink-0" aria-hidden />
          <span className={cn('truncate', collapsed ? 'sr-only' : 'sr-only xl:not-sr-only')}>{label}</span>
        </NavLink>
      </TooltipTrigger>
      <TooltipContent side="right" className={collapsed ? '' : 'xl:hidden'}>{label}</TooltipContent>
    </Tooltip>
  )
}
