import {
  BarChart3, Bell, Briefcase, CalendarDays, CalendarRange, CheckSquare, Clock, FileStack, FolderKanban, Gauge, Home, KanbanSquare,
  LayoutTemplate, ListChecks, Settings, UserCog, Users, type LucideIcon,
} from 'lucide-react'
import type { Me } from '@/lib/auth'

export interface NavItem {
  id: string
  path: string
  icon: LucideIcon
  label: string
  show: (me: Me) => boolean
  primary?: boolean
  /** Views of this item's page that highlight it too (§41 R4). */
  also?: string[]
  /** Additional parent aliases used only by the packet 052 one-list rail. */
  visualAlso?: string[] | ((me: Me, pathname: string) => string[])
  /** False for destinations the top bar already offers (the bell), kept here for breadcrumbs. */
  sidebar?: boolean
  /** The packet 052 rail placement; legacy `primary`/More placement remains when the flag is off. */
  visualGroup?: 'main' | 'foot'
  /** Resources is one visual destination while its permitted landing page varies by capability. */
  visualPath?: (me: Me) => string
}

const all = () => true

/** Global navigation (§9.4, §13.0, FR-VIS-01, §41 R4, decision E2). An item appears only when the user may open it and its
 *  destination works. Six items are primary; the rest sit under More, which stays collapsed unless the current page is
 *  there. Boards, Calendar, Portfolio and Workload are also reachable from their parent page, which they highlight. */
export const NAV: NavItem[] = [
  { id: 'mywork', path: '/my-work', icon: CheckSquare, label: 'nav.myWork', show: all, primary: true, visualGroup: 'main' },
  { id: 'projects', path: '/projects', icon: Briefcase, label: 'nav.projects', show: all, primary: true, also: ['/portfolio'], visualGroup: 'main' },
  { id: 'tasks', path: '/tasks', icon: ListChecks, label: 'nav.tasks', show: all, primary: true, also: ['/boards', '/calendar', '/timeline'], visualAlso: ['/pickup'], visualGroup: 'main' },
  { id: 'planner', path: '/planner', icon: FolderKanban, label: 'nav.resources', show: (m) => m.capabilities.workload, primary: true, also: ['/workload'], visualAlso: ['/vacation'], visualGroup: 'main' },
  { id: 'time', path: '/time', icon: Clock, label: 'nav.time', show: all, primary: true, visualGroup: 'main', visualAlso: (me) => me.capabilities.workload ? [] : ['/vacation'] },
  { id: 'reports', path: '/reports', icon: BarChart3, label: 'nav.reports', show: all, primary: true, visualGroup: 'main' },
  { id: 'home', path: '/home', icon: Home, label: 'nav.home', show: all, visualGroup: 'main' },
  { id: 'boards', path: '/boards', icon: KanbanSquare, label: 'nav.boards', show: all },
  { id: 'calendar', path: '/calendar', icon: CalendarDays, label: 'nav.calendar', show: all },
  { id: 'portfolio', path: '/portfolio', icon: Gauge, label: 'nav.portfolio', show: (m) => m.capabilities.portfolio },
  { id: 'workload', path: '/workload', icon: CalendarRange, label: 'nav.workload', show: (m) => m.capabilities.workload },
  { id: 'team', path: '/team', icon: Users, label: 'nav.team', show: all, visualAlso: ['/organisation'], visualGroup: 'main' },
  { id: 'files', path: '/files', icon: FileStack, label: 'nav.files', show: all, visualGroup: 'foot' },
  { id: 'vacation', path: '/vacation', icon: CalendarDays, label: 'availability.title', show: (me) => !me.capabilities.readOnly },
  { id: 'organisation', path: '/organisation', icon: Users, label: 'organisation.title', show: m => m.flags?.org_structure === true },
  { id: 'pickup', path: '/pickup', icon: ListChecks, label: 'pickup.title', show: m => m.flags?.task_pickup === true },
  { id: 'staff', path: '/staff', icon: UserCog, label: 'nav.staff', show: (m) => m.capabilities.staff, visualGroup: 'foot', visualAlso: (_, pathname) => /^\/people\/[^/]+\/reassign-work$/.test(pathname) ? [pathname] : [] },
  { id: 'notifications', path: '/notifications', icon: Bell, label: 'nav.notifications', show: all, sidebar: false },
  { id: 'templates', path: '/templates', icon: LayoutTemplate, label: 'nav.templates', show: (m) => m.capabilities.templates, visualGroup: 'foot' },
  { id: 'admin', path: '/admin', icon: Settings, label: 'nav.admin', show: (m) => m.capabilities.admin, visualGroup: 'foot' },
]

/** Routes whose packets are built; others stay hidden rather than becoming dead links (§36.1). */
export const BUILT = new Set<string>(['organisation', 'pickup', 'home', 'mywork', 'boards', 'projects', 'tasks', 'calendar', 'files', 'time', 'reports', 'team', 'portfolio', 'workload', 'planner', 'staff', 'notifications', 'templates', 'admin'])
