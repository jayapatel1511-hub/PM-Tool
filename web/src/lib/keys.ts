import type { QueryClient, QueryKey } from '@tanstack/react-query'

// Prefixes deliberately include related screens: extra refetches are safer than stale coordination data (§44.5).
export const roots = {
  project: ['project'], projects: ['projects'], myWork: ['mywork'], pulse: ['unread-pulse'],
  allocations: ['allocations'], workload: ['workload'], planningGrid: ['planning-grid'],
  planningEntries: ['planning-entries'], myWeek: ['my-week'], home: ['home'], workspaces: ['workspaces'], templates: ['templates'],
} as const

const taskKeys = ['task', 'wtasks', 'wboard', 'gantt', 'coordination', 'workload', 'planning-grid', 'planning-entries', 'my-week', 'mywork-tasks', 'open-work', 'open-items', 'time', 'time-tasks', 'report'] as const
const deliverableKeys = ['deliverable', 'gantt', 'coordination', 'coord-options', 'workspace-coordination', 'open-items', 'wtasks'] as const
const personNoticeKeys = ['person-facts', 'person-panel', 'staff', 'wteam', 'people', 'p', 'admin', 'coord-options', 'issue-verifier-eligible', 'pickup-offers', 'handoff-options', 'organisation', 'workspace-coordination', 'hr-identities'] as const
const boardFactKeys = ['task', 'wtasks', 'wboard'] as const
const teamFactKeys = ['wteam', 'p', 'person-panel'] as const

export const itemKeys: Readonly<Record<string, readonly string[]>> = {
  Task: taskKeys, Dependency: taskKeys, // a link changes both tasks' waiting and blocked state, the chain and the candidates under 'task'
  TaskContribution: ['task'], // a child contribution changes its parent's collaborator facts, never task workflow facts
  Deliverable: deliverableKeys, DeliverableDependency: deliverableKeys,
  Milestone: ['milestone', 'gantt', 'coordination', 'submission-milestones'], Decision: ['decision', 'decision-screen', 'coordination', 'basis-decision-options'],
  Action: ['action', 'coordination'], Risk: ['risk', 'coordination'], Issue: ['issue', 'issue-root-causes', 'coordination', 'issue-locations', 'issue-documents', 'issue-verification', 'issue-reference-impacts', 'review-issues'],
  OpenRole: ['allocations', 'open-roles', 'workload', 'planning-grid', 'planning-entries', 'my-week', 'report'],
  ResourceAllocation: ['allocations', 'allocation-detail', 'allocation-review-options', 'workload', 'planning-grid', 'planning-entries', 'my-week', 'report'],
  PlanningEntry: ['planning-grid', 'planning-entries', 'planning-entry-deeplink', 'my-week', 'workload', 'report'],
  PersonAvailabilityOverride: ['availability', 'availability-view', 'planning-grid', 'my-week', 'workload', 'time', 'report'],
  ApprovedLeave: ['availability', 'availability-view', 'planning-grid', 'my-week', 'workload', 'time', 'report'],
  PersonSchedule: ['availability', 'availability-view', 'planning-grid', 'my-week', 'workload', 'time', 'report'],
  ReviewPackage: ['reviews', 'review-detail', 'review-finding-context', 'coord-options', 'workspace-coordination'],
  ReviewFinding: ['reviews', 'review-detail', 'review-finding-context', 'coord-options', 'workspace-coordination'],
  ChangeNotice: ['changes', 'change-detail', 'change-assessment-context', 'coord-options', 'input-uses', 'workspace-coordination'],
  ChangeAssessment: ['changes', 'change-detail', 'change-assessment-context', 'coord-options', 'input-uses', 'workspace-coordination'],
  Handoff: ['handoffs', 'handoff-detail', 'handoff-options', 'coord-options', 'workspace-coordination'],
  SubmissionPackage: ['submissions', 'submission-detail', 'submission-check-context', 'submission-choices', 'coord-options', 'workspace-coordination'],
  DesignBasisEntry: ['design-basis', 'design-basis-detail'],
  CalendarEvent: ['calendar'], TimeEntry: ['time', 'report'], User: ['me', 'staff', 'reference', 'admin', 'organisation', 'organisation-placements', 'workload', 'planning-grid', 'hr-identities', 'time', 'report', ...personNoticeKeys, ...boardFactKeys], // 'hr-identities' is the import dialog's HR source and key list
  Comment: ['history', 'comments', 'task', 'wtasks', 'wboard', 'mywork-tasks', 'report'], DocumentLink: ['links', 'history', 'files'],
  TaskPickupOffer: ['pickup-offers', 'pickup-own-load', 'task', 'mywork-tasks', 'workload', 'planning-grid'],
  OrgLevel: ['admin', 'organisation', 'organisation-placements', 'staff', 'workload', 'planning-grid', 'report', 'person-panel'],
  OrgUnit: ['admin', 'organisation', 'organisation-placements', 'staff', 'workload', 'planning-grid', 'report', ...teamFactKeys],
  CommitmentMissCategory: ['admin', 'weekly-promise', 'readiness-detail'],
  AdminReview: ['admin'],
  UserPreferences: ['onboarding', 'me'], ReferenceData: ['reference', 'admin', 'report', 'deliverable', ...teamFactKeys, ...boardFactKeys], Setting: ['me', 'reference', 'admin', 'time', 'report', 'people-capacity', ...personNoticeKeys, ...boardFactKeys], // settings also change flag-owned reference lists, thresholds and organisation-local review ages
  // Person facts can appear in several permitted project Teams without a project-scoped command.
  PersonFacts: personNoticeKeys,
  UserCertification: ['person-facts', 'person-panel', 'staff', 'wteam', 'p', 'admin'],
  CertificationTag: ['certification-tags', 'person-facts', 'person-panel', 'staff', 'wteam', 'p', 'admin'],
  Holiday: ['admin', 'availability-view', 'planning-grid', 'my-week', 'workload', 'time', 'report'], // a statutory day changes every affected person's capacity
  Project: ['projects-pick', 'projects-pick-ids', 'projects-mine-live', 'planner-projects', 'planner-projects-filter', 'scope-projects', 'workspace-coordination', 'portfolio', 'workload', 'wteam', 'person-panel', 'files', 'calendar', 'time', 'report', ...boardFactKeys], // includes the board's project manager and workspace project identity
  Member: ['coord-options', 'issue-verifier-eligible', 'handoff-options', 'pickup-offers', 'staff', 'workload', 'workspace-coordination', 'open-items', 'time', 'report', ...teamFactKeys, ...boardFactKeys],
  Discipline: ['planner-project-disciplines', ...teamFactKeys, ...boardFactKeys], // canonical project-discipline audit type; lead/name changes refresh rendered board headers
  SavedView: ['views'],
  Template: ['templates', 'template', 'template-preview', 'template-pack', 'template-provenance'], // exact-version reads and informational project origins refresh with template changes
}

const coordination = ['reviews', 'review-detail', 'review-finding-context', 'changes', 'change-detail', 'change-assessment-context', 'coord-options', 'input-uses', 'handoffs', 'handoff-detail', 'submissions', 'submission-detail', 'submission-check-context', 'submission-choices', 'deliverable', 'workspace-coordination', 'coordination']
const readiness = ['commitment-reliability', 'weekly-promise', 'readiness-constraint', 'readiness-detail', 'readiness-constraints', 'readiness-prerequisites', 'readiness-assumptions', 'task-start', 'coordination', 'workspace-coordination']
const basis = ['design-basis', 'design-basis-detail', 'readiness-assumptions', 'basis-decision-options']

export function keysForChange(itemType?: string, projectId?: string, itemId?: string): QueryKey[] {
  // Enhanced notification action facts depend on the current record graph, even when a
  // muted project's activity does not change the personal notification pulse.
  const keys: QueryKey[] = [roots.project, roots.projects, roots.myWork, roots.home, ['my-work-follow-up'], ['notifications', 'screen'], ['task-history-actors'], ['search'], ['search-counts'], ['search-page'], ['review-open-findings'], ['closeout'], ['history'], ['comments'], ['links']]
  if (!itemType || ['Task', 'Handoff', 'Project', 'Member', 'User', 'Discipline', 'Setting'].includes(itemType)
    || /^(Readiness|WorkConstraint|WeeklyPlan|OutputCommitment)/.test(itemType))
    keys.push(['handoff-readiness-links'], ['weekly-readiness-agenda'])
  if (!itemType || ['UserPreferences', 'User', 'Setting'].includes(itemType)) keys.push(['notice-preferences'])
  // Files also reads current revisions, Task parents, author labels and current project rights.
  if (itemType && ['Task', 'Deliverable', 'User', 'Member', 'Discipline', 'ReferenceData', 'Setting'].includes(itemType)) keys.push(['files'])
  if (itemType && ['ExternalParty', 'ReferenceData', 'Setting', 'Project', 'Member', 'User'].includes(itemType)) keys.push(['decision-screen'])
  if (itemType && ['Task', 'Deliverable', 'Milestone', 'CalendarEvent', 'Project', 'Member', 'User', 'Discipline', 'ReferenceData', 'Setting', 'PersonAvailabilityOverride', 'ApprovedLeave', 'PersonSchedule', 'Holiday', 'OrgUnit', 'OrgLevel'].includes(itemType))
    keys.push(['calendar-screen'], ['calendar'])
  if (itemType && (/^(Review|DisciplineReview|Finding|Change|Input|Source|Handoff|Submission)/.test(itemType)
    || ['Deliverable', 'Project', 'Member', 'User', 'Discipline', 'ReferenceData', 'Setting'].includes(itemType)))
    keys.push(['review-discipline-summary'], ['review-default-suggestions'])
  if (itemType && ['ReferenceData', 'Discipline', 'Setting'].includes(itemType)) keys.push(['admin', 'deliverable-review-defaults'])
  if (itemType === 'Task') keys.push(['notifications'], ['unread'], ['unread-pulse'])
  if (itemType && ['Issue', 'ReferenceData', 'Setting', 'Project', 'Member', 'User'].includes(itemType)) keys.push(['issue-root-causes'])
  if (itemType && ['Project', 'Member', 'User', 'Setting'].includes(itemType)) keys.push(['templates'], ['template'], ['template-provenance'])
  if (projectId) keys.push(['p', projectId])
  if (!itemType) for (const root of new Set([...Object.values(itemKeys).flat(), ...coordination, ...readiness, ...basis, 'calendar-screen', 'calendar', 'review-discipline-summary', 'review-default-suggestions'])) keys.push([root])
  if (itemType && /^(Basis|DesignBasis)/.test(itemType)) for (const root of basis) keys.push([root])
  if (itemType?.startsWith('Issue')) for (const root of itemKeys.Issue) keys.push([root])
  if (itemType && /^(Readiness|WorkConstraint|WeeklyPlan|OutputCommitment|TaskStart)/.test(itemType)) for (const root of readiness) keys.push([root])
  if (itemType && /^(Review|DisciplineReview|Finding|Change|Input|Source|Handoff|Submission|CheckEvidence)/.test(itemType))
    for (const root of coordination) keys.push([root])
  if (itemType?.startsWith('Submission')) keys.push(['wtasks'])
  for (const root of itemKeys[itemType ?? ''] ?? []) keys.push([root])
  if (keys.some(key => key[0] === 'workload')) keys.push(['pickup-own-load'], ['people-capacity'])
  if (itemType && itemId) keys.push(['history', itemType, itemId], ['links', itemType, itemId], ['comments', itemType, itemId])
  return keys
}

export function invalidateChange(qc: QueryClient, itemType?: string, projectId?: string, itemId?: string) {
  return Promise.all(keysForChange(itemType, projectId, itemId).map(queryKey => qc.invalidateQueries({ queryKey })))
}

/** Private visit/open/seen commands refresh their readers without pretending a business item changed. */
export function invalidateWorkSeen(qc: QueryClient) {
  const readers = ['mywork', 'mywork-tasks', 'my-work-follow-up', 'task', 'deliverable', 'decision', 'action', 'risk', 'issue', 'handoff-detail', 'change-detail', 'project', 'workspace-coordination'] as const
  return Promise.all([
    ...readers.map(root => qc.invalidateQueries({ queryKey: [root] })),
    qc.invalidateQueries({ predicate: query => query.queryKey[0] === 'p' && ['discipline-coordination', 'attention'].includes(String(query.queryKey[2])) }),
  ])
}

/** A successful discussion command refreshes its originating project row even when live events are delayed. */
export const refreshComments = (qc: QueryClient, projectId: string) => invalidateChange(qc, 'Comment', projectId)

/** A person's own workspaces and dashboard layout have no item type, so the commands that change them name their roots here (FR-VIS-02, FR-VIS-07). */
export const refreshWorkspaces = (qc: QueryClient) => qc.invalidateQueries({ queryKey: roots.workspaces })
export const refreshHome = (qc: QueryClient) => qc.invalidateQueries({ queryKey: roots.home })

/** What the bell, the Notifications page, My Work's inbox and the Following tab read: a changed pulse stamp, a read marker or a
 *  mark-read reaches all of them; the count query is keyed by the pulse stamp, so its root is enough. */
export const notificationRoots = [['notifications'], ['following'], ['unread']] as const

export function invalidateNotifications(qc: QueryClient) {
  return Promise.all(notificationRoots.map(queryKey => qc.invalidateQueries({ queryKey })))
}

/** The Preferences page's own read: channels per event, the digest and the follow levels. */
export function invalidatePreferences(qc: QueryClient) {
  return qc.invalidateQueries({ queryKey: ['preferences'] })
}

/** Keep both passes: derived project state lands after the command (§23.5). */
export function refreshProject(qc: QueryClient, projectId?: string) {
  invalidateChange(qc, undefined, projectId)
  setTimeout(() => {
    for (const queryKey of [roots.project, roots.myWork, ...(projectId ? [['p', projectId]] : [])])
      void qc.invalidateQueries({ queryKey })
  }, 1500)
}
