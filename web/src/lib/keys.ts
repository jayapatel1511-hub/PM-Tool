import type { QueryClient, QueryKey } from '@tanstack/react-query'

// Prefixes deliberately include related screens: extra refetches are safer than stale coordination data (§44.5).
export const roots = {
  project: ['project'], projects: ['projects'], myWork: ['mywork'], pulse: ['unread-pulse'],
  allocations: ['allocations'], workload: ['workload'], planningGrid: ['planning-grid'],
  planningEntries: ['planning-entries'], myWeek: ['my-week'], home: ['home'], workspaces: ['workspaces'],
} as const

const taskKeys = ['task', 'wtasks', 'wboard', 'gantt', 'coordination', 'workload', 'planning-grid', 'planning-entries', 'my-week', 'mywork-tasks', 'open-work', 'open-items'] as const
const deliverableKeys = ['deliverable', 'gantt', 'coordination', 'coord-options', 'workspace-coordination', 'open-items'] as const

export const itemKeys: Readonly<Record<string, readonly string[]>> = {
  Task: taskKeys, Dependency: taskKeys, // a link changes both tasks' waiting and blocked state, the chain and the candidates under 'task'
  Deliverable: deliverableKeys, DeliverableDependency: deliverableKeys,
  Milestone: ['milestone', 'gantt', 'coordination', 'submission-milestones'], Decision: ['decision', 'coordination', 'basis-decision-options'],
  Action: ['action', 'coordination'], Risk: ['risk', 'coordination'], Issue: ['issue', 'coordination', 'issue-locations', 'issue-documents', 'issue-verification', 'issue-reference-impacts', 'review-issues'],
  OpenRole: ['allocations', 'open-roles', 'workload', 'planning-grid', 'planning-entries', 'my-week'],
  ResourceAllocation: ['allocations', 'allocation-detail', 'allocation-review-options', 'workload', 'planning-grid', 'planning-entries', 'my-week'],
  PlanningEntry: ['planning-grid', 'planning-entries', 'planning-entry-deeplink', 'my-week', 'workload'],
  PersonAvailabilityOverride: ['availability', 'availability-view', 'planning-grid', 'my-week', 'workload'],
  ApprovedLeave: ['availability', 'availability-view', 'planning-grid', 'my-week', 'workload'],
  PersonSchedule: ['availability', 'availability-view', 'planning-grid', 'my-week', 'workload'],
  ReviewPackage: ['reviews', 'review-detail', 'coord-options', 'workspace-coordination'],
  ReviewFinding: ['reviews', 'review-detail', 'coord-options', 'workspace-coordination'],
  ChangeNotice: ['changes', 'change-detail', 'coord-options', 'input-uses', 'workspace-coordination'],
  ChangeAssessment: ['changes', 'change-detail', 'coord-options', 'input-uses', 'workspace-coordination'],
  Handoff: ['handoffs', 'handoff-detail', 'handoff-options', 'coord-options', 'workspace-coordination'],
  SubmissionPackage: ['submissions', 'submission-detail', 'submission-choices', 'coord-options', 'workspace-coordination'],
  DesignBasisEntry: ['design-basis', 'design-basis-detail'],
  CalendarEvent: ['calendar'], TimeEntry: ['time'], User: ['me', 'staff', 'reference', 'admin', 'organisation', 'organisation-placements', 'workload', 'planning-grid', 'hr-identities'], // 'hr-identities' is the import dialog's HR source and key list
  Comment: ['history', 'comments', 'task'], DocumentLink: ['links', 'history', 'files'],
  TaskPickupOffer: ['pickup-offers', 'pickup-own-load', 'task', 'mywork-tasks', 'workload', 'planning-grid'],
  OrgLevel: ['admin', 'organisation', 'organisation-placements', 'staff', 'workload', 'planning-grid', 'report'],
  OrgUnit: ['admin', 'organisation', 'organisation-placements', 'staff', 'workload', 'planning-grid', 'report'],
  CommitmentMissCategory: ['admin', 'weekly-promise', 'readiness-detail'],
  AdminReview: ['admin'],
  UserPreferences: ['onboarding', 'me'], ReferenceData: ['reference', 'admin'], Setting: ['me', 'admin'], // 'admin' is the Administration lists: reference kinds, holidays, settings, users
  Holiday: ['admin', 'availability-view', 'planning-grid', 'my-week', 'workload'], // a statutory day changes every affected person's capacity
  Project: ['projects-pick', 'planner-projects-filter', 'workspace-coordination', 'portfolio', 'workload'],
  Member: ['coord-options', 'staff', 'workload', 'workspace-coordination', 'open-items'],
  Discipline: ['planner-project-disciplines'], // a project's discipline list, lead or active flag changes the planner entry's picker; the Team tab is under ['p', projectId]
  SavedView: ['views'],
}

const coordination = ['reviews', 'review-detail', 'changes', 'change-detail', 'coord-options', 'input-uses', 'handoffs', 'handoff-detail', 'submissions', 'submission-detail', 'submission-choices', 'deliverable', 'workspace-coordination', 'coordination']
const readiness = ['commitment-reliability', 'weekly-promise', 'readiness-constraint', 'readiness-detail', 'readiness-constraints', 'readiness-prerequisites', 'readiness-assumptions', 'task-start', 'coordination', 'workspace-coordination']
const basis = ['design-basis', 'design-basis-detail', 'readiness-assumptions', 'basis-decision-options']

export function keysForChange(itemType?: string, projectId?: string, itemId?: string): QueryKey[] {
  const keys: QueryKey[] = [roots.project, roots.projects, roots.myWork, roots.home, ['search'], ['search-counts'], ['search-page'], ['closeout'], ['history'], ['comments'], ['links']]
  if (projectId) keys.push(['p', projectId])
  if (!itemType) for (const root of new Set([...Object.values(itemKeys).flat(), ...coordination, ...readiness, ...basis])) keys.push([root])
  if (itemType && /^(Basis|DesignBasis)/.test(itemType)) for (const root of basis) keys.push([root])
  if (itemType?.startsWith('Issue')) for (const root of itemKeys.Issue) keys.push([root])
  if (itemType && /^(Readiness|WorkConstraint|WeeklyPlan|OutputCommitment|TaskStart)/.test(itemType)) for (const root of readiness) keys.push([root])
  if (itemType && /^(Review|DisciplineReview|Finding|Change|Input|Source|Handoff|Submission|CheckEvidence)/.test(itemType))
    for (const root of coordination) keys.push([root])
  for (const root of itemKeys[itemType ?? ''] ?? []) keys.push([root])
  if (keys.some(key => key[0] === 'workload')) keys.push(['pickup-own-load'])
  if (itemType && itemId) keys.push(['history', itemType, itemId], ['links', itemType, itemId], ['comments', itemType, itemId])
  return keys
}

export function invalidateChange(qc: QueryClient, itemType?: string, projectId?: string, itemId?: string) {
  return Promise.all(keysForChange(itemType, projectId, itemId).map(queryKey => qc.invalidateQueries({ queryKey })))
}

/** A person's own workspaces and dashboard layout have no item type, so the commands that change them name their roots here (FR-VIS-02, FR-VIS-07). */
export const refreshWorkspaces = (qc: QueryClient) => qc.invalidateQueries({ queryKey: roots.workspaces })
export const refreshHome = (qc: QueryClient) => qc.invalidateQueries({ queryKey: roots.home })

/** Keep both passes: derived project state lands after the command (§23.5). */
export function refreshProject(qc: QueryClient, projectId?: string) {
  invalidateChange(qc, undefined, projectId)
  setTimeout(() => {
    for (const queryKey of [roots.project, roots.myWork, ...(projectId ? [['p', projectId]] : [])])
      void qc.invalidateQueries({ queryKey })
  }, 1500)
}
