import type { RouteObject } from 'react-router'
import { Navigate } from 'react-router'
import { AdminIndex, AdminLayout, OrgActivity, ReferenceData, Settings, Users } from '@/pages/admin/Admin'
import { Operations } from '@/pages/admin/Operations'
import { Holidays } from '@/pages/admin/Holidays'
import { MyWorkPage } from '@/pages/MyWork'
import { NotFound } from '@/pages/NotFound'
import { ProjectActivityTab } from '@/pages/projects/ProjectActivity'
import { ProjectLayout } from '@/pages/projects/ProjectLayout'
import { ProjectListPage } from '@/pages/projects/ProjectList'
import { ProjectSettingsTab } from '@/pages/projects/ProjectSettings'
import { TeamTab } from '@/pages/projects/Team'
import { MilestonesTab } from '@/pages/projects/Milestones'
import { DeliverablesTab } from '@/pages/projects/Deliverables'
import { TasksTab } from '@/pages/projects/Tasks'
import { BoardTab } from '@/pages/projects/Board'
import { TaskPage } from '@/pages/projects/TaskPanel'
import { DecisionsTab } from '@/pages/projects/Decisions'
import { IssuesTab, RisksTab } from '@/pages/projects/Registers'
import { MeetingsTab } from '@/pages/projects/Meetings'
import { ReviewsTab } from '@/pages/projects/Reviews'
import { ChangesTab } from '@/pages/projects/Changes'
import { HandoffsTab } from '@/pages/projects/Handoffs'
import { SubmissionsTab } from '@/pages/projects/Submissions'
import { AllocationsTab } from '@/pages/projects/Allocations'
import { TimelineTab } from '@/pages/projects/Timeline'
import '@/pages/projects/CopyStructure'
import '@/pages/projects/FromTemplate'
import { TemplatePage, TemplatesPage } from '@/pages/Templates'
import { ReassignWorkPage } from '@/pages/ReassignWork'
import { PortfolioPage } from '@/pages/Portfolio'
import { WorkloadPage } from '@/pages/Workload'
import { CalendarPage } from '@/pages/Calendar'
import { TimePage } from '@/pages/Time'
import { HomePage } from '@/pages/Home'
import { WorkspaceBoardPage, WorkspaceTasksPage } from '@/pages/WorkspaceTasks'
import { GanttPage } from '@/pages/Gantt'
import { FilesPage } from '@/pages/Files'
import { WorkspaceTeamPage } from '@/pages/WorkspaceTeam'
import { ProjectScope } from '@/components/hub/workspace'
import { useCurrentProject } from '@/pages/projects/ProjectLayout'
import '@/components/hub/new-item'
import '@/pages/projects/Health'
import '@/pages/projects/Follow'
import '@/components/hub/comments'
import '@/components/hub/links'
import '@/components/hub/notifications'
import '@/components/hub/search'
import { SearchPage } from '@/pages/Search'
import { ReportPage, ReportsPage } from '@/pages/Reports'
import { NotificationsPage } from '@/pages/Notifications'
import { StaffPage } from '@/pages/Staff'
import { DashboardTab } from '@/pages/projects/Dashboard'
import { CoordinationTab } from '@/pages/projects/Coordination'
import { PreferencesPage } from '@/pages/Preferences'

const admin = (el: React.ReactNode) => <AdminLayout>{el}</AdminLayout>
/** A workspace view shown for the current project only (FR-VIS-01 project tabs). */
function InProject({ children }: { children: React.ReactNode }) {
  return <ProjectScope projectId={useCurrentProject().id}>{children}</ProjectScope>
}

/** Project tab routes added by later packets (dashboard, tasks, deliverables…). */
export const PROJECT_ROUTES: RouteObject[] = []
/** The tab a project opens on: the dashboard once packet 007 is in place. */
export const PROJECT_HOME = 'dashboard'

/** Route table; each packet adds its screens here. */
export const routes: RouteObject[] = [
  { index: true, element: <Navigate to="/my-work" replace /> },
  { path: 'my-work', element: <MyWorkPage /> },
  { path: 'home', element: <HomePage /> },
  { path: 'boards', element: <WorkspaceBoardPage /> },
  { path: 'tasks', element: <WorkspaceTasksPage /> },
  { path: 'timeline', element: <GanttPage /> },
  { path: 'files', element: <FilesPage /> },
  { path: 'team', element: <WorkspaceTeamPage /> },
  { path: 'projects', element: <ProjectListPage /> },
  {
    path: 'projects/:number', element: <ProjectLayout />, children: [
      { index: true, element: <Navigate to={PROJECT_HOME} replace /> },
      { path: 'dashboard', element: <DashboardTab /> },
      { path: 'coordination', element: <CoordinationTab /> },
      { path: 'handoffs', element: <HandoffsTab /> },
      { path: 'reviews', element: <ReviewsTab /> },
      { path: 'changes', element: <ChangesTab /> },
      { path: 'submissions', element: <SubmissionsTab /> },
      { path: 'allocations', element: <AllocationsTab /> },
      { path: 'tasks', element: <TasksTab /> },
      { path: 'board', element: <BoardTab /> },
      { path: 'milestones', element: <MilestonesTab /> },
      { path: 'timeline', element: <TimelineTab /> },
      { path: 'deliverables', element: <DeliverablesTab /> },
      { path: 'decisions', element: <DecisionsTab /> },
      { path: 'risks', element: <RisksTab /> },
      { path: 'issues', element: <IssuesTab /> },
      { path: 'meetings', element: <MeetingsTab /> },
      { path: 'files', element: <InProject><FilesPage /></InProject> },
      { path: 'calendar', element: <InProject><CalendarPage /></InProject> },
      { path: 'team', element: <TeamTab /> },
      { path: 'activity', element: <ProjectActivityTab /> },
      { path: 'settings', element: <ProjectSettingsTab /> },
      ...PROJECT_ROUTES,
    ],
  },
  { path: 'tasks/:id', element: <TaskPage /> },
  { path: 'notifications', element: <NotificationsPage /> },
  { path: 'search', element: <SearchPage /> },
  { path: 'reports', element: <ReportsPage /> },
  { path: 'reports/:code', element: <ReportPage /> },
  { path: 'staff', element: <StaffPage /> },
  { path: 'portfolio', element: <PortfolioPage /> },
  { path: 'workload', element: <WorkloadPage /> },
  { path: 'calendar', element: <CalendarPage /> },
  { path: 'time', element: <TimePage /> },
  { path: 'people/:id/reassign-work', element: <ReassignWorkPage /> },
  { path: 'preferences', element: <PreferencesPage /> },
  { path: 'templates', element: <TemplatesPage /> },
  { path: 'templates/:id', element: <TemplatePage /> },
  { path: 'admin', element: admin(<AdminIndex />) },
  { path: 'admin/reference/:kind', element: admin(<ReferenceData />) },
  { path: 'admin/settings', element: admin(<Settings />) },
  { path: 'admin/users', element: admin(<Users />) },
  { path: 'admin/activity', element: admin(<OrgActivity />) },
  { path: 'admin/operations', element: admin(<Operations />) },
  { path: 'admin/holidays', element: admin(<Holidays />) },
  { path: '*', element: <NotFound /> },
]
