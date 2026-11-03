import { useFlag } from '@/lib/auth-context'
import type { RouteObject } from 'react-router'
import { Navigate, useLocation } from 'react-router'
import { lazy, Suspense, type ComponentType } from 'react'
import { MyWorkPage } from '@/pages/MyWork'
import { ProjectLayout, useCurrentProject } from '@/pages/projects/ProjectLayout'
import { ProjectScope } from '@/components/hub/workspace'
import { Loading } from '@/components/hub/common'
import { lazyImport } from '@/lib/lazy'

type RoutedComponent<T extends ComponentType<any> = ComponentType<any>> = { routeLoad: () => Promise<{ default: T }> }
const route = <T extends ComponentType<any>>(load: () => Promise<{ default: T }>): RoutedComponent<T> => ({ routeLoad: load })
function Flagged({ name, children }: { name: string; children: React.ReactNode }) {
  return useFlag(name) ? children : <Suspense fallback={<Loading rows={6} />}><MissingPage /></Suspense>
}
const routed = (component: RoutedComponent, flag?: string) => ({
  lazy: async () => {
    const { default: Page } = await lazyImport(component.routeLoad)
    return { Component: flag ? () => <Flagged name={flag}><Page /></Flagged> : Page }
  },
  HydrateFallback: () => <Loading rows={6} />,
})
const AdminIndex = route(() => import('@/pages/admin/Admin').then(m => ({ default: m.AdminIndex })))
const AdminLayout = route(() => import('@/pages/admin/Admin').then(m => ({ default: m.AdminLayout })))
const OrgActivity = route(() => import('@/pages/admin/Admin').then(m => ({ default: m.OrgActivity })))
const ReferenceData = route(() => import('@/pages/admin/Admin').then(m => ({ default: m.ReferenceData })))
const Settings = route(() => import('@/pages/admin/Admin').then(m => ({ default: m.Settings })))
const Users = route(() => import('@/pages/admin/Admin').then(m => ({ default: m.Users })))
const Operations = route(() => import('@/pages/admin/Operations').then(m => ({ default: m.Operations })))
const Holidays = route(() => import('@/pages/admin/Holidays').then(m => ({ default: m.Holidays })))
const NotFound = route(() => import('@/pages/NotFound').then(m => ({ default: m.NotFound })))
const MissingPage = lazy(() => lazyImport(NotFound.routeLoad))
const PickupPage = route(() => import('@/pages/Pickup').then(m => ({ default: m.PickupPage })))
const OrganisationPage = route(() => import('@/pages/Organisation').then(m => ({ default: m.OrganisationPage })))
const StructureAdmin = route(() => import('@/pages/admin/Structure').then(m => ({ default: m.StructureAdmin })))
const AdminReview = route(() => import('@/pages/admin/AdminReview').then(m => ({ default: m.AdminReview })))
const AuditChains = route(() => import('@/pages/admin/AuditChains').then(m => ({ default: m.AuditChains })))
const HomePage = route(() => import('@/pages/Home').then(m => ({ default: m.HomePage })))
const WorkspaceBoardPage = route(() => import('@/pages/WorkspaceTasks').then(m => ({ default: m.WorkspaceBoardPage })))
const WorkspaceTasksPage = route(() => import('@/pages/WorkspaceTasks').then(m => ({ default: m.WorkspaceTasksPage })))
const GanttPage = route(() => import('@/pages/Gantt').then(m => ({ default: m.GanttPage })))
const FilesPage = route(() => import('@/pages/Files').then(m => ({ default: m.FilesPage })))
const WorkspaceTeamPage = route(() => import('@/pages/WorkspaceTeam').then(m => ({ default: m.WorkspaceTeamPage })))
const ProjectListPage = route(() => import('@/pages/projects/ProjectList').then(m => ({ default: m.ProjectListPage })))
const ProjectSettingsTab = route(() => import('@/pages/projects/ProjectSettings').then(m => ({ default: m.ProjectSettingsTab })))
const TeamTab = route(() => import('@/pages/projects/Team').then(m => ({ default: m.TeamTab })))
const MilestonesTab = route(() => import('@/pages/projects/Milestones').then(m => ({ default: m.MilestonesTab })))
const DeliverablesTab = route(() => import('@/pages/projects/Deliverables').then(m => ({ default: m.DeliverablesTab })))
const TasksTab = route(() => import('@/pages/projects/Tasks').then(m => ({ default: m.TasksTab })))
const BoardTab = route(() => import('@/pages/projects/Board').then(m => ({ default: m.BoardTab })))
const TaskPage = route(() => import('@/pages/projects/TaskPanel').then(m => ({ default: m.TaskPage })))
const DecisionsTab = route(() => import('@/pages/projects/Decisions').then(m => ({ default: m.DecisionsTab })))
const IssuesTab = route(() => import('@/pages/projects/Registers').then(m => ({ default: m.IssuesTab })))
const RisksTab = route(() => import('@/pages/projects/Registers').then(m => ({ default: m.RisksTab })))
const MeetingsTab = route(() => import('@/pages/projects/Meetings').then(m => ({ default: m.MeetingsTab })))
const ReviewsTab = route(() => import('@/pages/projects/Reviews').then(m => ({ default: m.ReviewsTab })))
const ChangesTab = route(() => import('@/pages/projects/Changes').then(m => ({ default: m.ChangesTab })))
const HandoffsTab = route(() => import('@/pages/projects/Handoffs').then(m => ({ default: m.HandoffsTab })))
const SubmissionsTab = route(() => import('@/pages/projects/Submissions').then(m => ({ default: m.SubmissionsTab })))
const AllocationsTab = route(() => import('@/pages/projects/Allocations').then(m => ({ default: m.AllocationsTab })))
const DesignBasisTab = route(() => import('@/pages/projects/DesignBasis').then(m => ({ default: m.DesignBasisTab })))
const TimelineTab = route(() => import('@/pages/projects/Timeline').then(m => ({ default: m.TimelineTab })))
const TemplatePage = route(() => import('@/pages/Templates').then(m => ({ default: m.TemplatePage })))
const TemplatesPage = route(() => import('@/pages/Templates').then(m => ({ default: m.TemplatesPage })))
const ReassignWorkPage = route(() => import('@/pages/ReassignWork').then(m => ({ default: m.ReassignWorkPage })))
const PortfolioPage = route(() => import('@/pages/Portfolio').then(m => ({ default: m.PortfolioPage })))
const WorkloadPage = route(() => import('@/pages/Workload').then(m => ({ default: m.WorkloadPage })))
const VacationPage = route(() => import('@/pages/Vacation').then(m => ({ default: m.VacationPage })))
const CalendarPage = route(() => import('@/pages/Calendar').then(m => ({ default: m.CalendarPage })))
const TimePage = route(() => import('@/pages/Time').then(m => ({ default: m.TimePage })))
const SearchPage = route(() => import('@/pages/Search').then(m => ({ default: m.SearchPage })))
const ReportsPage = route(() => import('@/pages/Reports').then(m => ({ default: m.ReportsPage })))
const ReportPage = route(() => import('@/pages/Reports').then(m => ({ default: m.ReportPage })))
const NotificationsPage = route(() => import('@/pages/Notifications').then(m => ({ default: m.NotificationsPage })))
const StaffPage = route(() => import('@/pages/Staff').then(m => ({ default: m.StaffPage })))
const PreferencesPage = route(() => import('@/pages/Preferences').then(m => ({ default: m.PreferencesPage })))
const DashboardTab = route(() => import('@/pages/projects/Dashboard').then(m => ({ default: m.DashboardTab })))
const CoordinationTab = route(() => import('@/pages/projects/Coordination').then(m => ({ default: m.CoordinationTab })))
const ReadinessTab = route(() => import('@/pages/projects/Readiness').then(m => ({ default: m.ReadinessTab })))
const ProjectActivityTab = route(() => import('@/pages/projects/ProjectActivity').then(m => ({ default: m.ProjectActivityTab })))
const PlannerPage = route(() => import('@/pages/Planner').then(m => ({ default: m.PlannerPage })))

const admin = (component: RoutedComponent, flag?: string) => ({ HydrateFallback: () => <Loading rows={6} />, lazy: async () => {
  const [{ default: Layout }, { default: Page }] = await Promise.all([
    lazyImport(AdminLayout.routeLoad), lazyImport(component.routeLoad),
  ])
  return { Component: () => flag ? <Flagged name={flag}><Layout><Page /></Layout></Flagged> : <Layout><Page /></Layout> }
} })
const inProject = (component: RoutedComponent) => ({ HydrateFallback: () => <Loading rows={6} />, lazy: async () => {
  const { default: Page } = await lazyImport(component.routeLoad)
  return { Component: () => <InProject><Page /></InProject> }
} })
/** A workspace view shown for the current project only (FR-VIS-01 project tabs). */
function InProject({ children }: { children: React.ReactNode }) {
  return <ProjectScope projectId={useCurrentProject().id}>{children}</ProjectScope>
}

/** Project tab routes added by later packets (dashboard, tasks, deliverables…). */
export const PROJECT_ROUTES: RouteObject[] = []
/** The tab a project opens on: the dashboard once packet 007 is in place. */
export const PROJECT_HOME = 'dashboard'

/** The project landing alias keeps the copied query, anchor and source-return context. */
function ProjectIndex() {
  const location = useLocation()
  return <Navigate to={{ pathname: PROJECT_HOME, search: location.search, hash: location.hash }} state={location.state} replace />
}

/** Route table; each packet adds its screens here. */
export const routes: RouteObject[] = [
  { index: true, element: <Navigate to="/my-work" replace /> },
  { path: 'pickup', ...routed(PickupPage, 'task_pickup') },
  { path: 'organisation', ...routed(OrganisationPage, 'org_structure') },
  { path: 'admin/structure', ...admin(StructureAdmin, 'org_structure') },
  { path: 'admin/audit-chains', ...admin(AuditChains, 'verifiable_log') },
  { path: 'admin/review', ...admin(AdminReview, 'admin_review') },
  { path: 'my-work', element: <MyWorkPage /> },
  { path: 'home', ...routed(HomePage) },
  { path: 'boards', ...routed(WorkspaceBoardPage) },
  { path: 'tasks', ...routed(WorkspaceTasksPage) },
  { path: 'timeline', ...routed(GanttPage) },
  { path: 'files', ...routed(FilesPage) },
  { path: 'team', ...routed(WorkspaceTeamPage) },
  { path: 'projects', ...routed(ProjectListPage) },
  {
    path: 'projects/:number', element: <ProjectLayout />, children: [
      { index: true, element: <ProjectIndex /> },
      { path: 'dashboard', ...routed(DashboardTab) },
      { path: 'coordination', ...routed(CoordinationTab) },
      { path: 'readiness', ...routed(ReadinessTab) },
      { path: 'handoffs', ...routed(HandoffsTab) },
      { path: 'reviews', ...routed(ReviewsTab) },
      { path: 'changes', ...routed(ChangesTab) },
      { path: 'submissions', ...routed(SubmissionsTab) },
      { path: 'allocations', ...routed(AllocationsTab) },
      { path: 'design-basis', ...routed(DesignBasisTab) },
      { path: 'tasks', ...routed(TasksTab) },
      { path: 'board', ...routed(BoardTab) },
      { path: 'milestones', ...routed(MilestonesTab) },
      { path: 'timeline', ...routed(TimelineTab) },
      { path: 'deliverables', ...routed(DeliverablesTab) },
      { path: 'decisions', ...routed(DecisionsTab) },
      { path: 'risks', ...routed(RisksTab) },
      { path: 'issues', ...routed(IssuesTab) },
      { path: 'meetings', ...routed(MeetingsTab) },
      { path: 'files', ...inProject(FilesPage) },
      { path: 'vacation', ...routed(VacationPage) },
  { path: 'calendar', ...inProject(CalendarPage) },
      { path: 'team', ...routed(TeamTab) },
      { path: 'activity', ...routed(ProjectActivityTab) },
      { path: 'settings', ...routed(ProjectSettingsTab) },
      ...PROJECT_ROUTES,
    ],
  },
  { path: 'tasks/:id', ...routed(TaskPage) },
  { path: 'notifications', ...routed(NotificationsPage) },
  { path: 'search', ...routed(SearchPage) },
  { path: 'reports', ...routed(ReportsPage) },
  { path: 'reports/:code', ...routed(ReportPage) },
  { path: 'staff', ...routed(StaffPage) },
  { path: 'portfolio', ...routed(PortfolioPage) },
  { path: 'workload', ...routed(WorkloadPage) },
  { path: 'planner', ...routed(PlannerPage) },
  { path: 'vacation', ...routed(VacationPage) },
  { path: 'calendar', ...routed(CalendarPage) },
  { path: 'time', ...routed(TimePage) },
  { path: 'people/:id/reassign-work', ...routed(ReassignWorkPage) },
  { path: 'preferences', ...routed(PreferencesPage) },
  { path: 'templates', ...routed(TemplatesPage) },
  { path: 'templates/:id', ...routed(TemplatePage) },
  { path: 'admin', ...admin(AdminIndex) },
  { path: 'admin/reference/:kind', ...admin(ReferenceData) },
  { path: 'admin/settings', ...admin(Settings) },
  { path: 'admin/users', ...admin(Users) },
  { path: 'admin/activity', ...admin(OrgActivity) },
  { path: 'admin/operations', ...admin(Operations) },
  { path: 'admin/holidays', ...admin(Holidays) },
  { path: '*', ...routed(NotFound) },
]
