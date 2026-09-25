import {
  BarChart3, Bell, Briefcase, CalendarDays, CheckSquare, Clock, FileStack, FolderKanban, Gauge, Home, KanbanSquare,
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
}

const all = () => true

/** Global navigation (§9.4, §13.0, FR-VIS-01). An item appears only when the user may open it and
 *  its destination works; lower-frequency routes sit under More. */
export const NAV: NavItem[] = [
  { id: 'home', path: '/home', icon: Home, label: 'nav.home', show: all, primary: true },
  { id: 'mywork', path: '/my-work', icon: CheckSquare, label: 'nav.myWork', show: all, primary: true },
  { id: 'boards', path: '/boards', icon: KanbanSquare, label: 'nav.boards', show: all, primary: true },
  { id: 'projects', path: '/projects', icon: Briefcase, label: 'nav.projects', show: all, primary: true },
  { id: 'tasks', path: '/tasks', icon: ListChecks, label: 'nav.tasks', show: all, primary: true },
  { id: 'calendar', path: '/calendar', icon: CalendarDays, label: 'nav.calendar', show: all, primary: true },
  { id: 'files', path: '/files', icon: FileStack, label: 'nav.files', show: all, primary: true },
  { id: 'time', path: '/time', icon: Clock, label: 'nav.time', show: all, primary: true },
  { id: 'reports', path: '/reports', icon: BarChart3, label: 'nav.reports', show: all, primary: true },
  { id: 'team', path: '/team', icon: Users, label: 'nav.team', show: all, primary: true },
  { id: 'portfolio', path: '/portfolio', icon: Gauge, label: 'nav.portfolio', show: (m) => m.capabilities.portfolio },
  { id: 'workload', path: '/workload', icon: FolderKanban, label: 'nav.workload', show: (m) => m.capabilities.workload },
  { id: 'staff', path: '/staff', icon: UserCog, label: 'nav.staff', show: (m) => m.capabilities.staff },
  { id: 'notifications', path: '/notifications', icon: Bell, label: 'nav.notifications', show: all },
  { id: 'templates', path: '/templates', icon: LayoutTemplate, label: 'nav.templates', show: (m) => m.capabilities.templates },
  { id: 'admin', path: '/admin', icon: Settings, label: 'nav.admin', show: (m) => m.capabilities.admin },
]

/** Routes whose packets are built; others stay hidden rather than becoming dead links (§36.1). */
export const BUILT = new Set<string>(['home', 'mywork', 'boards', 'projects', 'tasks', 'calendar', 'files', 'time', 'reports', 'team', 'portfolio', 'workload', 'staff', 'notifications', 'templates', 'admin'])
